using API_PORTAL.Data;
using API_PORTAL.DTO.PublicRegistries;
using API_PORTAL.Errors;
using API_PORTAL.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_PORTAL.Controllers.PublicRegistries;

[ApiController]
[Route("api/public/registries")]
[AllowAnonymous]
public sealed class PublicRegistriesPublicController : ControllerBase
{
    private readonly PortalDbContext db;
    private readonly SubmissionFileStorageService storage;
    private readonly SubmissionFileDownloadTokenService downloadTokens;

    public PublicRegistriesPublicController(
        PortalDbContext db,
        SubmissionFileStorageService storage,
        SubmissionFileDownloadTokenService downloadTokens)
    {
        this.db = db;
        this.storage = storage;
        this.downloadTokens = downloadTokens;
    }

    [HttpGet]
    public async Task<ActionResult<PagedPublicRegistryResponse>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return UnprocessableEntity(ApiProblemDetails.CreateValidation(
                HttpContext, 422, "Paginare invalidă", "Parametrii de paginare sunt invalizi.",
                new Dictionary<string, string[]>
                {
                    ["page"] = page < 1 ? ["Pagina trebuie să fie cel puțin 1."] : [],
                    ["pageSize"] = pageSize is < 1 or > 100 ? ["pageSize trebuie să fie între 1 și 100."] : []
                }));
        }

        var query = db.PublicRegistrations.AsNoTracking().Where(item => item.is_published);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(item => item.display_order).ThenBy(item => item.name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new PublicRegistryListItemResponse(
                item.id, item.code, item.name, item.description, item.is_published,
                item.display_order,
                db.PublicRegistryEntries.Count(entry => entry.registry_id == item.id && entry.is_published)))
            .ToListAsync(cancellationToken);
        return Ok(new PagedPublicRegistryResponse(items, page, pageSize, total));
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<PublicRegistryResponse>> GetByCode(
        string code,
        [FromQuery] string? search = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100 || from.HasValue && to.HasValue && from > to)
        {
            return UnprocessableEntity(ApiProblemDetails.CreateValidation(
                HttpContext, 422, "Filtre invalid", "Filtrele și paginarea sunt invalide.",
                new Dictionary<string, string[]> { ["page"] = page < 1 ? ["Pagina trebuie să fie cel puțin 1."] : [], ["pageSize"] = pageSize is < 1 or > 100 ? ["pageSize trebuie să fie între 1 și 100."] : [], ["date"] = from.HasValue && to.HasValue && from > to ? ["Intervalul de date este invalid."] : [] }));
        }

        var registry = await db.PublicRegistrations.AsNoTracking().SingleOrDefaultAsync(item => item.code == code.Trim().ToLowerInvariant() && item.is_published, cancellationToken);
        if (registry is null) return NotFound(ApiProblemDetails.Create(HttpContext, 404, "Registru indisponibil", "Registrul public nu există sau nu este publicat."));

        var query = db.PublicRegistryEntries.AsNoTracking().Where(item => item.registry_id == registry.id && item.is_published);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.title.Contains(term) || item.position_number.Contains(term));
        }
        if (from.HasValue) query = query.Where(item => item.entry_date >= from.Value);
        if (to.HasValue) query = query.Where(item => item.entry_date <= to.Value);

        var entryIds = await query.OrderByDescending(item => item.entry_date).ThenBy(item => item.position_number)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(item => item.id).ToListAsync(cancellationToken);
        var entries = new List<PublicRegistryEntryResponse>(entryIds.Count);
        foreach (var entryId in entryIds) entries.Add(await ToEntryResponseAsync(entryId, cancellationToken));
        return Ok(new PublicRegistryResponse(registry.id, registry.code, registry.name, registry.description, true, registry.display_order, entries));
    }

    [HttpGet("documents/{documentId:guid}/download")]
    public async Task<IActionResult> Download(
        Guid documentId,
        [FromQuery] string? token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || !downloadTokens.TryValidate(token, documentId, out _))
        {
            return StatusCode(403, ApiProblemDetails.Create(HttpContext, 403, "URL invalid sau expirat", "URL-ul de descărcare nu mai este valid."));
        }

        var document = await db.PublicRegistryDocuments.AsNoTracking()
            .Include(item => item.entry)
            .ThenInclude(item => item.registry)
            .SingleOrDefaultAsync(item => item.id == documentId && item.entry.is_published && item.entry.registry.is_published, cancellationToken);
        if (document is null) return NotFound(ApiProblemDetails.Create(HttpContext, 404, "Document inexistent", "Documentul public nu există."));

        try
        {
            var stream = storage.OpenRead(document.storage_key);
            return File(stream, document.content_type, document.original_name, enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound(ApiProblemDetails.Create(HttpContext, 404, "Fișier inexistent", "Fișierul nu mai există în storage."));
        }
    }

    private async Task<PublicRegistryEntryResponse> ToEntryResponseAsync(Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await db.PublicRegistryEntries.AsNoTracking().SingleAsync(item => item.id == entryId, cancellationToken);
        var documents = await db.PublicRegistryDocuments.AsNoTracking().Where(item => item.entry_id == entryId).OrderBy(item => item.display_order).ThenBy(item => item.original_name).ToListAsync(cancellationToken);
        return new PublicRegistryEntryResponse(entry.id, entry.position_number, entry.title, entry.entry_date, entry.description, true, documents.Select(document =>
        {
            var token = downloadTokens.Create(document.id).Token;
            return new PublicRegistryDocumentResponse(document.id, document.original_name, document.content_type, document.size_bytes, document.display_order, $"/api/public/registries/documents/{document.id}/download?token={Uri.EscapeDataString(token)}");
        }).ToList());
    }
}
