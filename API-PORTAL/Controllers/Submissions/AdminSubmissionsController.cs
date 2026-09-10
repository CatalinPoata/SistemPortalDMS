using API_PORTAL.Data;
using API_PORTAL.DTO.Submissions;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using API_PORTAL.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_PORTAL.Controllers.Submissions;

[ApiController]
[Route("api/admin/submissions")]
[Authorize(Roles = nameof(Role.Admin))]
public sealed class AdminSubmissionsController(PortalDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedAdminSubmissionResponse>> GetAll(
        [FromQuery] string? serviceCode = null,
        [FromQuery] SubmissionStatus? status = null,
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        [FromQuery] string? applicant = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1) errors["page"] = ["Pagina trebuie să fie cel puțin 1."];
        if (pageSize is < 1 or > 100) errors["pageSize"] = ["Dimensiunea paginii trebuie să fie între 1 și 100."];
        if (dateFrom is not null && dateTo is not null && dateFrom > dateTo)
        {
            errors["dateTo"] = ["Data de sfârșit trebuie să fie după data de început."];
        }

        if (errors.Count > 0)
        {
            return UnprocessableEntity(ApiProblemDetails.CreateValidation(
                HttpContext,
                StatusCodes.Status422UnprocessableEntity,
                "Filtre invalide",
                "Verifică filtrele trimise pentru lista cererilor.",
                errors));
        }

        var normalizedServiceCode = serviceCode?.Trim().ToLowerInvariant();
        var normalizedApplicant = applicant?.Trim().ToLowerInvariant();
        var query = db.Submissions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(normalizedServiceCode))
        {
            query = query.Where(item => item.service.code == normalizedServiceCode);
        }

        if (status.HasValue)
        {
            query = query.Where(item => item.status == status.Value);
        }

        if (dateFrom.HasValue)
        {
            var from = StartOfDay(dateFrom.Value);
            query = query.Where(item => item.submitted_at >= from);
        }

        if (dateTo.HasValue)
        {
            var to = StartOfDay(dateTo.Value.AddDays(1));
            query = query.Where(item => item.submitted_at < to);
        }

        if (!string.IsNullOrWhiteSpace(normalizedApplicant))
        {
            query = query.Where(item =>
                item.user.full_name.ToLower().Contains(normalizedApplicant) ||
                item.user.email.ToLower().Contains(normalizedApplicant));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.submitted_at)
            .ThenBy(item => item.id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AdminSubmissionListItemResponse(
                item.id,
                item.external_id,
                item.service.code,
                item.service.title,
                item.status,
                item.user.full_name,
                item.user.email,
                item.submitted_at,
                item.registered_at,
                item.registry_display_number))
            .ToListAsync(cancellationToken);

        return Ok(new PagedAdminSubmissionResponse(items, page, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminSubmissionDetailsResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var submission = await db.Submissions
            .AsNoTracking()
            .Include(item => item.service)
            .Include(item => item.user)
            .SingleOrDefaultAsync(item => item.id == id, cancellationToken);

        if (submission is null)
        {
            return NotFound(ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status404NotFound,
                "Cerere inexistentă",
                "Cererea solicitată nu există."));
        }

        var events = await db.SubmissionEvents
            .AsNoTracking()
            .Where(item => item.submission_id == submission.id)
            .OrderBy(item => item.occurred_at)
            .Select(item => new SubmissionEventResponse(
                item.id,
                item.occurred_at,
                item.type,
                item.message,
                item.file_id))
            .ToListAsync(cancellationToken);

        var files = await db.SubmissionFiles
            .AsNoTracking()
            .Where(item => item.submission_id == submission.id)
            .OrderBy(item => item.original_name)
            .Select(item => new SubmissionFileResponse(
                item.id,
                item.field_key,
                item.kind,
                item.original_name,
                item.content_type,
                item.size_bytes,
                item.sha256))
            .ToListAsync(cancellationToken);

        return Ok(new AdminSubmissionDetailsResponse(
            submission.id,
            submission.external_id,
            submission.service.code,
            submission.service.title,
            submission.schema_version,
            submission.user.full_name,
            submission.user.email,
            submission.status,
            submission.status_details,
            submission.submitted_at,
            submission.registry_number,
            submission.registry_year,
            submission.registry_display_number,
            submission.registered_at,
            submission.dms_entry_id,
            submission.form_snapshot.RootElement.Clone(),
            submission.values.RootElement.Clone(),
            files,
            events));
    }

    private static DateTimeOffset StartOfDay(DateOnly date) => new(
        date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
}
