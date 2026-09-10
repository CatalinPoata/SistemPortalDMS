using API_PORTAL.Data;
using API_PORTAL.DTO.Appointments;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using API_PORTAL.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Claims;

namespace API_PORTAL.Controllers.PublicAppointments
{
    [ApiController]
    [Route("api/public/appointment-types")]
    public sealed class PublicAppointmentTypesController : ControllerBase
    {
        private readonly PortalDbContext db;

        public PublicAppointmentTypesController(PortalDbContext db)
        {
            this.db = db;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<PagedAppointmentTypeResponse>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 24,
            CancellationToken cancellationToken = default)
        {
            if (page < 1 || pageSize is < 1 or > 100)
            {
                return InvalidPage(page, pageSize);
            }

            var query = db.AppointmentTypes
                .AsNoTracking()
                .Where(item => item.is_active);
            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(item => item.name)
                .ThenBy(item => item.code)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(item => new AppointmentTypeResponse(
                    item.id,
                    item.code,
                    item.name,
                    item.description,
                    item.location,
                    item.duration_minutes,
                    item.requires_confirmation,
                    item.max_days_ahead,
                    item.is_active,
                    item.created_at,
                    item.updated_at))
                .ToListAsync(cancellationToken);

            return Ok(new PagedAppointmentTypeResponse(
                items,
                page,
                pageSize,
                total));
        }

        [HttpGet("{code}/slots")]
        [AllowAnonymous]
        public async Task<ActionResult<PagedAppointmentSlotResponse>> GetSlots(
            string code,
            [FromQuery] DateTimeOffset? from = null,
            [FromQuery] DateTimeOffset? to = null,
            [FromQuery] DateOnly? date = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100,
            CancellationToken cancellationToken = default)
        {
            if (page < 1 || pageSize is < 1 or > 100 ||
                from is not null && to is not null && from > to)
            {
                return InvalidSlotQuery(page, pageSize, from, to);
            }

            var type = await db.AppointmentTypes.AsNoTracking()
                .SingleOrDefaultAsync(item => item.code == code && item.is_active,
                    cancellationToken);
            if (type is null)
            {
                return NotFound(ApiProblemDetails.Create(
                    HttpContext, StatusCodes.Status404NotFound,
                    "Tip de programare indisponibil",
                    "Tipul de programare nu există sau nu este activ."));
            }

            var now = DateTimeOffset.UtcNow;
            var latest = now.AddDays(type.max_days_ahead);
            var query = db.AppointmentSlots.AsNoTracking().Where(slot =>
                slot.appointment_type_id == type.id &&
                slot.starts_at > now && slot.starts_at <= latest &&
                !slot.is_blocked && slot.booked_count < slot.capacity);
            if (date is not null)
            {
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(
                    "Europe/Bucharest");
                var dayStart = ToUtc(date.Value, TimeOnly.MinValue, timeZone);
                var nextDayStart = ToUtc(
                    date.Value.AddDays(1),
                    TimeOnly.MinValue,
                    timeZone);
                query = query.Where(slot =>
                    slot.starts_at >= dayStart && slot.starts_at < nextDayStart);
            }

            if (from is not null) query = query.Where(slot => slot.starts_at >= from.Value);
            if (to is not null) query = query.Where(slot => slot.starts_at <= to.Value);

            var total = await query.CountAsync(cancellationToken);
            var items = await query.OrderBy(slot => slot.starts_at)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(slot => new AppointmentSlotResponse(
                    slot.id, slot.appointment_type_id, slot.starts_at, slot.ends_at,
                    slot.capacity, slot.booked_count, slot.is_blocked))
                .ToListAsync(cancellationToken);
            return Ok(new PagedAppointmentSlotResponse(items, page, pageSize, total));
        }

        private static DateTimeOffset ToUtc(
            DateOnly date,
            TimeOnly time,
            TimeZoneInfo timeZone)
        {
            var local = DateTime.SpecifyKind(
                date.ToDateTime(time),
                DateTimeKind.Unspecified);
            return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
                local,
                timeZone));
        }

        [HttpPost("{code}/appointments")]
        [Authorize(Roles = nameof(Role.Citizen))]
        public async Task<ActionResult<AppointmentResponse>> CreateAppointment(
            string code,
            CreateAppointmentRequest request,
            CancellationToken cancellationToken)
        {
            var userIdValue = User.FindFirstValue(
                System.Security.Claims.ClaimTypes.NameIdentifier) ??
                User.FindFirstValue("sub");
            if (!Guid.TryParse(userIdValue, out var userId))
            {
                return Forbid();
            }

            var user = await db.Users.SingleOrDefaultAsync(
                item => item.id == userId,
                cancellationToken);
            if (user is null || !user.email_confirmed || !user.is_active)
            {
                return UnprocessableEntity(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Contul nu poate solicita programări",
                    "Adresa de e-mail trebuie confirmată înainte de programare."));
            }

            var type = await db.AppointmentTypes.SingleOrDefaultAsync(
                item => item.code == code && item.is_active,
                cancellationToken);
            var slot = type is null
                ? null
                : await db.AppointmentSlots
                    .Include(item => item.appointment_type)
                    .SingleOrDefaultAsync(
                        item => item.id == request.SlotId &&
                            item.appointment_type_id == type.id,
                        cancellationToken);

            if (slot is null)
            {
                return NotFound(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    "Interval indisponibil",
                    "Tipul sau intervalul de programare nu există."));
            }

            var now = DateTimeOffset.UtcNow;
            if (slot.starts_at <= now ||
                slot.starts_at > now.AddDays(type!.max_days_ahead))
            {
                return UnprocessableEntity(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Interval în afara perioadei permise",
                    "Intervalul nu mai poate fi solicitat la această dată."));
            }

            var duplicate = await db.Appointments.AnyAsync(item =>
                item.slot_id == slot.id && item.user_id == userId &&
                item.status != AppointmentStatus.Cancelled,
                cancellationToken);
            if (duplicate)
            {
                return Conflict(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status409Conflict,
                    "Programare duplicată",
                    "Utilizatorul are deja o programare pentru acest interval."));
            }

            await using var transaction = db.Database.ProviderName?
                .Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true
                ? null
                : await db.Database.BeginTransactionAsync(cancellationToken);

            if (db.Database.ProviderName?.Contains(
                    "InMemory", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (slot.is_blocked || slot.booked_count >= slot.capacity)
                {
                    return Conflict(SlotConflict());
                }
                slot.booked_count++;
            }
            else
            {
                var updated = await db.AppointmentSlots
                    .Where(item => item.id == slot.id &&
                        !item.is_blocked && item.booked_count < item.capacity)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(
                        item => item.booked_count,
                        item => item.booked_count + 1), cancellationToken);

                if (updated != 1)
                {
                    return Conflict(SlotConflict());
                }
            }

            var appointment = new Appointment
            {
                id = Guid.NewGuid(),
                slot_id = slot.id,
                user_id = userId,
                status = type.requires_confirmation
                    ? AppointmentStatus.Requested
                    : AppointmentStatus.Confirmed,
                notes = string.IsNullOrWhiteSpace(request.Notes)
                    ? null : request.Notes.Trim(),
                reference_code = CreateReferenceCode()
            };
            db.Appointments.Add(appointment);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation
                })
            {
                return Conflict(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status409Conflict,
                    "Programare duplicată",
                    "Utilizatorul are deja o programare pentru acest interval."));
            }
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            appointment.slot = slot;
            return StatusCode(
                StatusCodes.Status201Created,
                ToResponse(appointment));
        }

        private static string CreateReferenceCode() =>
            Convert.ToHexString(Guid.NewGuid().ToByteArray())[..12];

        private static AppointmentResponse ToResponse(Appointment appointment) =>
            new(
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

        private ProblemDetails SlotConflict() => ApiProblemDetails.Create(
            HttpContext,
            StatusCodes.Status409Conflict,
            "Interval indisponibil",
            "Intervalul este plin sau blocat.");

        private ActionResult InvalidPage(int page, int pageSize)
        {
            var errors = new Dictionary<string, string[]>();

            if (page < 1)
            {
                errors["page"] = ["Pagina trebuie să fie cel puțin 1."];
            }

            if (pageSize is < 1 or > 100)
            {
                errors["pageSize"] =
                ["pageSize trebuie să fie între 1 și 100."];
            }

            return UnprocessableEntity(ApiProblemDetails.CreateValidation(
                HttpContext,
                StatusCodes.Status422UnprocessableEntity,
                "Paginare invalidă",
                "Parametrii de paginare sunt invalizi.",
                errors));
        }

        private ActionResult InvalidSlotQuery(
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
                "Interval invalid",
                "Parametrii intervalului sunt invalizi.",
                errors));
        }
    }
}
