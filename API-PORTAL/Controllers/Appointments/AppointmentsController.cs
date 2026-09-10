using API_PORTAL.Data;
using API_PORTAL.DTO.Appointments;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using API_PORTAL.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace API_PORTAL.Controllers.Appointments
{
    [ApiController]
    [Route("api/appointments")]
    public sealed class AppointmentsController : ControllerBase
    {
        private readonly PortalDbContext db;

        public AppointmentsController(PortalDbContext db)
        {
            this.db = db;
        }

        [HttpGet]
        [Authorize(Roles = nameof(Role.Admin))]
        public async Task<ActionResult<PagedAppointmentResponse>> GetAll(
            [FromQuery] string? typeCode = null,
            [FromQuery] AppointmentStatus? status = null,
            [FromQuery] DateTimeOffset? from = null,
            [FromQuery] DateTimeOffset? to = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken cancellationToken = default)
        {
            if (page < 1 || pageSize is < 1 or > 100 ||
                from is not null && to is not null && from > to)
            {
                return InvalidQuery(page, pageSize, from, to);
            }

            var query = db.Appointments.AsNoTracking()
                .Where(item => typeCode == null ||
                    item.slot.appointment_type.code == typeCode)
                .Where(item => status == null || item.status == status)
                .Where(item => from == null || item.slot.starts_at >= from)
                .Where(item => to == null || item.slot.starts_at <= to);

            var total = await query.CountAsync(cancellationToken);
            var appointments = await query
                .Include(item => item.slot)
                .ThenInclude(slot => slot.appointment_type)
                .OrderBy(item => item.slot.starts_at)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Ok(new PagedAppointmentResponse(
                appointments.Select(ToResponse).ToList(),
                page,
                pageSize,
                total));
        }

        [HttpGet("mine")]
        [Authorize(Roles = nameof(Role.Citizen))]
        public async Task<ActionResult<IReadOnlyList<AppointmentResponse>>> Mine(
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Forbid();
            }

            var appointments = await db.Appointments.AsNoTracking()
                .Include(item => item.slot)
                .ThenInclude(slot => slot.appointment_type)
                .Where(item => item.user_id == userId)
                .OrderByDescending(item => item.slot.starts_at)
                .ToListAsync(cancellationToken);

            return Ok(appointments.Select(ToResponse).ToList());
        }

        [HttpPost("{id:guid}/cancel")]
        [Authorize(Roles = nameof(Role.Citizen))]
        public async Task<ActionResult<AppointmentResponse>> Cancel(
            Guid id,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Forbid();
            }

            var appointment = await db.Appointments
                .Include(item => item.slot)
                .Include(item => item.slot.appointment_type)
                .SingleOrDefaultAsync(
                    item => item.id == id && item.user_id == userId,
                    cancellationToken);

            if (appointment is null)
            {
                return NotFound(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    "Programare inexistentă",
                    "Programarea nu există în contul curent."));
            }

            if (appointment.status is not (
                    AppointmentStatus.Requested or AppointmentStatus.Confirmed) ||
                appointment.slot.starts_at <= DateTimeOffset.UtcNow)
            {
                return UnprocessableEntity(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Programarea nu poate fi anulată",
                    "Programarea a început sau nu mai poate fi anulată."));
            }

            await using var transaction = await BeginTransactionIfRelationalAsync(
                cancellationToken);

            if (IsInMemory())
            {
                appointment.slot.booked_count = Math.Max(
                    0,
                    appointment.slot.booked_count - 1);
            }
            else
            {
                var updated = await db.AppointmentSlots
                    .Where(slot => slot.id == appointment.slot_id &&
                        slot.booked_count > 0)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(
                        slot => slot.booked_count,
                        slot => slot.booked_count - 1), cancellationToken);

                if (updated != 1)
                {
                    return Conflict(SlotConflict());
                }
            }

            appointment.status = AppointmentStatus.Cancelled;
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return Ok(ToResponse(appointment));
        }

        [HttpPost("{id:guid}/confirm")]
        [Authorize(Roles = nameof(Role.Admin))]
        public Task<ActionResult<AppointmentResponse>> Confirm(
            Guid id,
            CancellationToken cancellationToken)
        {
            return ChangeStatusAsync(
                id,
                AppointmentStatus.Confirmed,
                decisionNote: null,
                releaseCapacity: false,
                cancellationToken);
        }

        [HttpPost("{id:guid}/reject")]
        [Authorize(Roles = nameof(Role.Admin))]
        public async Task<ActionResult<AppointmentResponse>> Reject(
            Guid id,
            AppointmentDecisionRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.DecisionNote))
            {
                return UnprocessableEntity(ApiProblemDetails.CreateValidation(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Motiv obligatoriu",
                    "Respingerea programării necesită un motiv.",
                    new Dictionary<string, string[]>
                    {
                        ["decisionNote"] = ["Motivul este obligatoriu."]
                    }));
            }

            return await ChangeStatusAsync(
                id,
                AppointmentStatus.Rejected,
                request.DecisionNote.Trim(),
                releaseCapacity: true,
                cancellationToken);
        }

        [HttpPost("{id:guid}/complete")]
        [Authorize(Roles = nameof(Role.Admin))]
        public Task<ActionResult<AppointmentResponse>> Complete(
            Guid id,
            CancellationToken cancellationToken)
        {
            return ChangeStatusAsync(
                id,
                AppointmentStatus.Completed,
                decisionNote: null,
                releaseCapacity: false,
                cancellationToken);
        }

        [HttpPost("{id:guid}/no-show")]
        [Authorize(Roles = nameof(Role.Admin))]
        public Task<ActionResult<AppointmentResponse>> NoShow(
            Guid id,
            CancellationToken cancellationToken)
        {
            return ChangeStatusAsync(
                id,
                AppointmentStatus.NoShow,
                decisionNote: null,
                releaseCapacity: false,
                cancellationToken);
        }

        private async Task<ActionResult<AppointmentResponse>> ChangeStatusAsync(
            Guid id,
            AppointmentStatus targetStatus,
            string? decisionNote,
            bool releaseCapacity,
            CancellationToken cancellationToken)
        {
            var appointment = await db.Appointments
                .Include(item => item.slot)
                .ThenInclude(slot => slot.appointment_type)
                .SingleOrDefaultAsync(item => item.id == id, cancellationToken);

            if (appointment is null)
            {
                return NotFound(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    "Programare inexistentă",
                    "Programarea nu există."));
            }

            if (!IsAllowedTransition(appointment.status, targetStatus))
            {
                return UnprocessableEntity(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Tranziție invalidă",
                    $"Programarea nu poate trece din {appointment.status} în {targetStatus}."));
            }

            await using var transaction = await BeginTransactionIfRelationalAsync(
                cancellationToken);

            if (releaseCapacity)
            {
                if (IsInMemory())
                {
                    appointment.slot.booked_count = Math.Max(
                        0,
                        appointment.slot.booked_count - 1);
                }
                else
                {
                    var updated = await db.AppointmentSlots
                        .Where(slot => slot.id == appointment.slot_id &&
                            slot.booked_count > 0)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(
                            slot => slot.booked_count,
                            slot => slot.booked_count - 1), cancellationToken);

                    if (updated != 1)
                    {
                        return Conflict(SlotConflict());
                    }
                }
            }

            appointment.status = targetStatus;
            appointment.decision_note = decisionNote;
            appointment.decided_at = DateTimeOffset.UtcNow;
            appointment.decided_by_user_id = GetRequiredUserId();
            await db.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return Ok(ToResponse(appointment));
        }

        private Guid GetRequiredUserId()
        {
            return Guid.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                User.FindFirstValue("sub")!);
        }

        private static bool IsAllowedTransition(
            AppointmentStatus current,
            AppointmentStatus target)
        {
            return (current, target) switch
            {
                (AppointmentStatus.Requested, AppointmentStatus.Confirmed) => true,
                (AppointmentStatus.Requested, AppointmentStatus.Rejected) => true,
                (AppointmentStatus.Confirmed, AppointmentStatus.Completed) => true,
                (AppointmentStatus.Confirmed, AppointmentStatus.NoShow) => true,
                _ => false
            };
        }

        private ActionResult InvalidQuery(
            int page,
            int pageSize,
            DateTimeOffset? from,
            DateTimeOffset? to)
        {
            var errors = new Dictionary<string, string[]>();
            if (page < 1) errors["page"] = ["Pagina trebuie să fie cel puțin 1."];
            if (pageSize is < 1 or > 100) errors["pageSize"] = ["pageSize trebuie să fie între 1 și 100."];
            if (from is not null && to is not null && from > to)
                errors["to"] = ["Sfârșitul intervalului trebuie să fie după început."];

            return UnprocessableEntity(ApiProblemDetails.CreateValidation(
                HttpContext,
                StatusCodes.Status422UnprocessableEntity,
                "Filtre invalide",
                "Parametrii de filtrare sunt invalizi.",
                errors));
        }

        private bool TryGetUserId(out Guid userId)
        {
            return Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                User.FindFirstValue("sub"),
                out userId);
        }

        private bool IsInMemory() => db.Database.ProviderName?
            .Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true;

        private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?>
            BeginTransactionIfRelationalAsync(CancellationToken cancellationToken)
        {
            return IsInMemory()
                ? null
                : await db.Database.BeginTransactionAsync(cancellationToken);
        }

        private static AppointmentResponse ToResponse(Appointment appointment)
        {
            return new AppointmentResponse(
                appointment.id,
                appointment.slot_id,
                appointment.slot.appointment_type.code,
                appointment.slot.appointment_type.name,
                appointment.slot.starts_at,
                appointment.slot.ends_at,
                appointment.status,
                appointment.notes,
                appointment.decision_note,
                appointment.reference_code);
        }

        private ProblemDetails SlotConflict() => ApiProblemDetails.Create(
            HttpContext,
            StatusCodes.Status409Conflict,
            "Interval indisponibil",
            "Intervalul este plin sau blocat.");
    }
}
