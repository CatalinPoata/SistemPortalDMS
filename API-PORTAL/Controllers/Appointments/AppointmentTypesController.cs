using API_PORTAL.Data;
using API_PORTAL.DTO.Appointments;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using API_PORTAL.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.RegularExpressions;

namespace API_PORTAL.Controllers.Appointments
{
    [ApiController]
    [Route("api/appointment-types")]
    [Authorize(Roles = nameof(Role.Admin))]
    public sealed class AppointmentTypesController : ControllerBase
    {
        private static readonly Regex CodePattern = new(
            "^[a-z0-9]+(?:-[a-z0-9]+)*$",
            RegexOptions.CultureInvariant);

        private readonly PortalDbContext db;

        public AppointmentTypesController(PortalDbContext db)
        {
            this.db = db;
        }

        [HttpGet]
        public async Task<ActionResult<PagedAppointmentTypeResponse>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken cancellationToken = default)
        {
            if (page < 1 || pageSize is < 1 or > 100)
            {
                return InvalidPage(page, pageSize);
            }

            var query = db.AppointmentTypes.AsNoTracking();
            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(item => item.name)
                .ThenBy(item => item.code)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(item => ToResponse(item))
                .ToListAsync(cancellationToken);

            return Ok(new PagedAppointmentTypeResponse(
                items,
                page,
                pageSize,
                total));
        }

        [HttpGet("{code}")]
        public async Task<ActionResult<AppointmentTypeResponse>> GetByCode(
            string code,
            CancellationToken cancellationToken)
        {
            var type = await FindByCodeAsync(code, cancellationToken);

            return type is null
                ? NotFound(TypeNotFound())
                : Ok(ToResponse(type));
        }

        [HttpPost]
        public async Task<ActionResult<AppointmentTypeResponse>> Create(
            CreateAppointmentTypeRequest request,
            CancellationToken cancellationToken)
        {
            var code = request.Code?.Trim().ToLowerInvariant() ?? string.Empty;
            var errors = ValidateWriteRequest(
                code,
                request.Name,
                request.DurationMinutes,
                request.MaxDaysAhead);

            if (errors.Count > 0)
            {
                return ValidationFailure(errors);
            }

            if (await db.AppointmentTypes.AnyAsync(
                    item => item.code == code,
                    cancellationToken))
            {
                return Conflict(DuplicateCode());
            }

            var type = new AppointmentType
            {
                id = Guid.NewGuid(),
                code = code,
                name = request.Name!.Trim(),
                description = Normalize(request.Description),
                location = Normalize(request.Location),
                duration_minutes = request.DurationMinutes,
                requires_confirmation = request.RequiresConfirmation,
                max_days_ahead = request.MaxDaysAhead,
                is_active = request.IsActive
            };

            db.AppointmentTypes.Add(type);

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
                return Conflict(DuplicateCode());
            }

            return CreatedAtAction(
                nameof(GetByCode),
                new { code = type.code },
                ToResponse(type));
        }

        [HttpPut("{code}")]
        public async Task<ActionResult<AppointmentTypeResponse>> Update(
            string code,
            UpdateAppointmentTypeRequest request,
            CancellationToken cancellationToken)
        {
            var type = await FindByCodeAsync(code, cancellationToken);

            if (type is null)
            {
                return NotFound(TypeNotFound());
            }

            var errors = ValidateWriteRequest(
                type.code,
                request.Name,
                request.DurationMinutes,
                request.MaxDaysAhead);

            if (errors.Count > 0)
            {
                return ValidationFailure(errors);
            }

            type.name = request.Name!.Trim();
            type.description = Normalize(request.Description);
            type.location = Normalize(request.Location);
            type.duration_minutes = request.DurationMinutes;
            type.requires_confirmation = request.RequiresConfirmation;
            type.max_days_ahead = request.MaxDaysAhead;
            type.is_active = request.IsActive;

            await db.SaveChangesAsync(cancellationToken);
            return Ok(ToResponse(type));
        }

        [HttpDelete("{code}")]
        public async Task<IActionResult> Delete(
            string code,
            CancellationToken cancellationToken)
        {
            var type = await FindByCodeAsync(code, cancellationToken);

            if (type is null)
            {
                return NotFound(TypeNotFound());
            }

            var hasSlots = await db.AppointmentSlots.AnyAsync(
                item => item.appointment_type_id == type.id,
                cancellationToken);

            if (hasSlots)
            {
                return Conflict(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status409Conflict,
                    "Tipul de programare are intervale",
                    "Un tip de programare cu intervale nu poate fi șters."));
            }

            db.AppointmentTypes.Remove(type);
            await db.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        [HttpGet("{code}/slots")]
        public async Task<ActionResult<PagedAppointmentSlotResponse>>
            GetSlots(
                string code,
                [FromQuery] DateTimeOffset? from = null,
                [FromQuery] DateTimeOffset? to = null,
                [FromQuery] int page = 1,
                [FromQuery] int pageSize = 100,
                CancellationToken cancellationToken = default)
        {
            var type = await FindByCodeAsync(code, cancellationToken);

            if (type is null)
            {
                return NotFound(TypeNotFound());
            }

            if (page < 1 || pageSize is < 1 or > 100 ||
                from is not null && to is not null && from > to)
            {
                return InvalidSlotQuery(page, pageSize, from, to);
            }

            var query = db.AppointmentSlots.AsNoTracking().Where(
                slot => slot.appointment_type_id == type.id);

            if (from is not null)
            {
                query = query.Where(slot => slot.starts_at >= from.Value);
            }

            if (to is not null)
            {
                query = query.Where(slot => slot.starts_at <= to.Value);
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query.OrderBy(slot => slot.starts_at)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(slot => ToSlotResponse(slot))
                .ToListAsync(cancellationToken);

            return Ok(new PagedAppointmentSlotResponse(
                items, page, pageSize, total));
        }

        [HttpPost("{code}/slots/generate")]
        public async Task<ActionResult<IReadOnlyList<AppointmentSlotResponse>>>
            GenerateSlots(
                string code,
                GenerateAppointmentSlotsRequest request,
                CancellationToken cancellationToken)
        {
            var type = await FindByCodeAsync(code, cancellationToken);

            if (type is null)
            {
                return NotFound(TypeNotFound());
            }

            var validation = ValidateGenerationRequest(request);
            if (validation.Errors.Count > 0)
            {
                return ValidationFailure(validation.Errors);
            }

            TimeZoneInfo timeZone;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(
                    request.TimeZoneId!.Trim());
            }
            catch (TimeZoneNotFoundException)
            {
                return ValidationFailure(new Dictionary<string, string[]>
                {
                    ["timeZoneId"] = ["Fusul orar nu este recunoscut."]
                });
            }
            catch (InvalidTimeZoneException)
            {
                return ValidationFailure(new Dictionary<string, string[]>
                {
                    ["timeZoneId"] = ["Fusul orar este invalid."]
                });
            }

            var candidates = BuildSlots(type, request, timeZone, validation);
            if (validation.Errors.Count > 0)
            {
                return ValidationFailure(validation.Errors);
            }

            var firstStart = candidates.Min(slot => slot.starts_at);
            var lastStart = candidates.Max(slot => slot.starts_at);
            var existingStarts = await db.AppointmentSlots
                .Where(slot => slot.appointment_type_id == type.id &&
                    slot.starts_at >= firstStart && slot.starts_at <= lastStart)
                .Select(slot => slot.starts_at)
                .ToListAsync(cancellationToken);

            var existing = existingStarts.ToHashSet();
            var created = candidates
                .Where(slot => !existing.Contains(slot.starts_at))
                .ToList();

            db.AppointmentSlots.AddRange(created);

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
                    "Intervale create simultan",
                    "Reîncarcă intervalele înainte de a genera din nou."));
            }

            return Ok(created.Select(ToSlotResponse).ToList());
        }

        [HttpPut("{code}/slots/{slotId:guid}/block")]
        public async Task<ActionResult<AppointmentSlotResponse>> SetBlocked(
            string code,
            Guid slotId,
            SetAppointmentSlotBlockRequest request,
            CancellationToken cancellationToken)
        {
            var type = await FindByCodeAsync(code, cancellationToken);
            if (type is null)
            {
                return NotFound(SlotNotFound());
            }

            var slot = await db.AppointmentSlots.SingleOrDefaultAsync(
                item => item.id == slotId &&
                    item.appointment_type_id == type.id,
                cancellationToken);
            if (slot is null) return NotFound(SlotNotFound());

            slot.is_blocked = request.IsBlocked;
            await db.SaveChangesAsync(cancellationToken);
            return Ok(ToSlotResponse(slot));
        }

        [HttpDelete("{code}/slots/{slotId:guid}")]
        public async Task<IActionResult> DeleteSlot(
            string code,
            Guid slotId,
            CancellationToken cancellationToken)
        {
            var type = await FindByCodeAsync(code, cancellationToken);
            if (type is null)
            {
                return NotFound(SlotNotFound());
            }

            var slot = await db.AppointmentSlots.SingleOrDefaultAsync(
                item => item.id == slotId && item.appointment_type_id == type.id,
                cancellationToken);
            if (slot is null)
            {
                return NotFound(SlotNotFound());
            }

            var hasAppointments = slot.booked_count > 0 ||
                await db.Appointments.AnyAsync(
                    item => item.slot_id == slot.id,
                    cancellationToken);
            if (hasAppointments)
            {
                return Conflict(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status409Conflict,
                    "Interval ocupat",
                    "Intervalul nu poate fi șters deoarece există programări " +
                    "asociate. Blochează-l pentru a opri rezervările noi."));
            }

            db.AppointmentSlots.Remove(slot);
            await db.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        private Task<AppointmentType?> FindByCodeAsync(
            string code,
            CancellationToken cancellationToken)
        {
            return db.AppointmentTypes.SingleOrDefaultAsync(
                item => item.code == code,
                cancellationToken);
        }

        private static AppointmentTypeResponse ToResponse(
            AppointmentType type)
        {
            return new AppointmentTypeResponse(
                type.id,
                type.code,
                type.name,
                type.description,
                type.location,
                type.duration_minutes,
                type.requires_confirmation,
                type.max_days_ahead,
                type.is_active,
                type.created_at,
                type.updated_at);
        }

        private ActionResult ValidationFailure(
            IReadOnlyDictionary<string, string[]> errors)
        {
            return UnprocessableEntity(ApiProblemDetails.CreateValidation(
                HttpContext,
                StatusCodes.Status422UnprocessableEntity,
                "Tip de programare invalid",
                "Unul sau mai multe câmpuri sunt invalide.",
                errors.ToDictionary(item => item.Key, item => item.Value)));
        }

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

            return ValidationFailure(errors);
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
            if (from is not null && to is not null && from > to) errors["to"] = ["Sfârșitul intervalului trebuie să fie după început."];
            return ValidationFailure(errors);
        }

        private static (Dictionary<string, string[]> Errors, HashSet<int> Weekdays)
            ValidateGenerationRequest(GenerateAppointmentSlotsRequest request)
        {
            var errors = new Dictionary<string, string[]>();
            var weekdays = request.Weekdays?.ToHashSet() ?? [];

            if (request.StartDate is null || request.EndDate is null ||
                request.StartDate > request.EndDate)
                errors["endDate"] = ["Data de sfârșit trebuie să fie după data de început."];
            if (request.DayStartsAt is null || request.DayEndsAt is null ||
                request.DayStartsAt >= request.DayEndsAt)
                errors["dayEndsAt"] = ["Ora de sfârșit trebuie să fie după ora de început."];
            if (weekdays.Count == 0 || weekdays.Any(day => day is < 1 or > 7))
                errors["weekdays"] = ["Zilele săptămânii sunt între 1 (Luni) și 7 (Duminică)."];
            if (request.Capacity < 1)
                errors["capacity"] = ["Capacitatea trebuie să fie cel puțin 1."];
            if (string.IsNullOrWhiteSpace(request.TimeZoneId))
                errors["timeZoneId"] = ["Fusul orar este obligatoriu."];

            return (errors, weekdays);
        }

        private static List<AppointmentSlot> BuildSlots(
            AppointmentType type,
            GenerateAppointmentSlotsRequest request,
            TimeZoneInfo timeZone,
            (Dictionary<string, string[]> Errors, HashSet<int> Weekdays) validation)
        {
            var slots = new List<AppointmentSlot>();
            var date = request.StartDate!.Value;
            var endDate = request.EndDate!.Value;

            while (date <= endDate)
            {
                var weekday = ((int)date.DayOfWeek + 6) % 7 + 1;
                if (validation.Weekdays.Contains(weekday))
                {
                    var dayStart = date.ToDateTime(
                        request.DayStartsAt!.Value,
                        DateTimeKind.Unspecified);
                    var dayEnd = date.ToDateTime(
                        request.DayEndsAt!.Value,
                        DateTimeKind.Unspecified);

                    if (timeZone.IsInvalidTime(dayStart) ||
                        timeZone.IsInvalidTime(dayEnd))
                    {
                        validation.Errors["startDate"] =
                        ["Programul include o oră inexistentă în fusul ales."];
                        return slots;
                    }

                    for (var start = dayStart;
                        start.AddMinutes(type.duration_minutes) <= dayEnd;
                        start = start.AddMinutes(type.duration_minutes))
                    {
                        var end = start.AddMinutes(type.duration_minutes);
                        slots.Add(new AppointmentSlot
                        {
                            id = Guid.NewGuid(),
                            appointment_type_id = type.id,
                            starts_at = new DateTimeOffset(
                                TimeZoneInfo.ConvertTimeToUtc(start, timeZone)),
                            ends_at = new DateTimeOffset(
                                TimeZoneInfo.ConvertTimeToUtc(end, timeZone)),
                            capacity = request.Capacity,
                            booked_count = 0,
                            is_blocked = false
                        });
                    }
                }
                date = date.AddDays(1);
            }

            if (slots.Count == 0 && validation.Errors.Count == 0)
                validation.Errors["dayEndsAt"] = ["Programul nu conține niciun interval complet."];

            return slots;
        }

        private static AppointmentSlotResponse ToSlotResponse(
            AppointmentSlot slot) => new(
                slot.id, slot.appointment_type_id, slot.starts_at, slot.ends_at,
                slot.capacity, slot.booked_count, slot.is_blocked);

        private static IReadOnlyDictionary<string, string[]>
            ValidateWriteRequest(
                string code,
                string? name,
                int durationMinutes,
                int maxDaysAhead)
        {
            var errors = new Dictionary<string, string[]>();

            if (!CodePattern.IsMatch(code))
            {
                errors["code"] =
                ["Codul poate conține litere mici, cifre și cratimă."];
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                errors["name"] = ["Denumirea este obligatorie."];
            }

            if (durationMinutes < 1)
            {
                errors["durationMinutes"] =
                ["Durata trebuie să fie cel puțin un minut."];
            }

            if (maxDaysAhead < 0)
            {
                errors["maxDaysAhead"] =
                ["Numărul de zile nu poate fi negativ."];
            }

            return errors;
        }

        private ProblemDetails TypeNotFound()
        {
            return ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status404NotFound,
                "Tip de programare inexistent",
                "Tipul de programare nu există.");
        }

        private ProblemDetails DuplicateCode()
        {
            return ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status409Conflict,
                "Cod de programare duplicat",
                "Există deja un tip de programare cu acest cod.");
        }

        private ProblemDetails SlotNotFound() => ApiProblemDetails.Create(
            HttpContext,
            StatusCodes.Status404NotFound,
            "Interval inexistent",
            "Intervalul de programare nu există pentru acest tip.");

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
