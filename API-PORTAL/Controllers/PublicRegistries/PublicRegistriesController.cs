using API_PORTAL.Data;
using API_PORTAL.DTO.PublicRegistries;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using API_PORTAL.Errors;
using API_PORTAL.Services;
using API_PORTAL.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.RegularExpressions;

namespace API_PORTAL.Controllers.PublicRegistries;

[ApiController]
[Route("api/public-registries")]
[Authorize(Roles = nameof(Role.Admin))]
public sealed class PublicRegistriesController : ControllerBase
{
    private static readonly Regex CodePattern = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant);
    private readonly PortalDbContext db;
    private readonly IServiceHtmlSanitizer sanitizer;
    private readonly SubmissionFileStorageService storage;
    private readonly SubmissionFileDownloadTokenService downloadTokens;

    public PublicRegistriesController(
        PortalDbContext db,
        IServiceHtmlSanitizer sanitizer,
        SubmissionFileStorageService storage,
        SubmissionFileDownloadTokenService downloadTokens)
    {
        this.db = db;
        this.sanitizer = sanitizer;
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
            return PageError(page, pageSize);
        }

        var query = db.PublicRegistrations.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(item => item.display_order).ThenBy(item => item.name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new PublicRegistryListItemResponse(
                item.id, item.code, item.name, item.description, item.is_published,
                item.display_order,
                db.PublicRegistryEntries.Count(entry => entry.registry_id == item.id)))
            .ToListAsync(cancellationToken);
        return Ok(new PagedPublicRegistryResponse(items, page, pageSize, total));
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<PublicRegistryResponse>> GetByCode(string code, CancellationToken cancellationToken)
    {
        var registry = await FindAsync(code, cancellationToken);
        return registry is null ? NotFound(NotFoundProblem()) : Ok(await ToResponseAsync(registry, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<PublicRegistryResponse>> Create(
        CreatePublicRegistryRequest request,
        CancellationToken cancellationToken)
    {
        var code = request.Code?.Trim().ToLowerInvariant() ?? string.Empty;
        var errors = ValidateRegistry(code, request.Name, request.DisplayOrder);
        if (errors.Count > 0) return ValidationFailure(errors);
        if (await db.PublicRegistrations.AnyAsync(item => item.code == code, cancellationToken)) return Conflict(DuplicateCode());

        var registry = new PublicRegistry
        {
            id = Guid.NewGuid(),
            code = code,
            name = request.Name!.Trim(),
            description = sanitizer.Sanitize(request.Description),
            display_order = request.DisplayOrder,
            is_published = false
        };
        db.PublicRegistrations.Add(registry);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsUnique(exception)) { return Conflict(DuplicateCode()); }
        return CreatedAtAction(nameof(GetByCode), new { code }, await ToResponseAsync(registry, cancellationToken));
    }

    [HttpPut("{code}")]
    public async Task<ActionResult<PublicRegistryResponse>> Update(
        string code,
        UpdatePublicRegistryRequest request,
        CancellationToken cancellationToken)
    {
        var registry = await FindAsync(code, cancellationToken);
        if (registry is null) return NotFound(NotFoundProblem());
        var errors = ValidateRegistry(registry.code, request.Name, request.DisplayOrder);
        if (errors.Count > 0) return ValidationFailure(errors);
        registry.name = request.Name!.Trim();
        registry.description = sanitizer.Sanitize(request.Description);
        registry.display_order = request.DisplayOrder;
        registry.is_published = request.IsPublished;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await ToResponseAsync(registry, cancellationToken));
    }

    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code, CancellationToken cancellationToken)
    {
        var registry = await FindAsync(code, cancellationToken);
        if (registry is null) return NotFound(NotFoundProblem());
        db.PublicRegistrations.Remove(registry);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{code}/publish")]
    public async Task<ActionResult<PublicRegistryResponse>> Publish(string code, CancellationToken cancellationToken)
    {
        var registry = await FindAsync(code, cancellationToken);
        if (registry is null) return NotFound(NotFoundProblem());
        if (!await db.PublicRegistryEntries.AnyAsync(item => item.registry_id == registry.id && item.is_published, cancellationToken))
        {
            return UnprocessableEntity(ApiProblemDetails.Create(HttpContext, 422, "Registru fără poziții", "Publică cel puțin o poziție înainte de publicarea registrului."));
        }
        registry.is_published = true;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await ToResponseAsync(registry, cancellationToken));
    }

    [HttpPost("{code}/unpublish")]
    public async Task<ActionResult<PublicRegistryResponse>> Unpublish(string code, CancellationToken cancellationToken)
    {
        var registry = await FindAsync(code, cancellationToken);
        if (registry is null) return NotFound(NotFoundProblem());
        registry.is_published = false;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await ToResponseAsync(registry, cancellationToken));
    }

    [HttpGet("{code}/entries")]
    public async Task<ActionResult<IReadOnlyList<PublicRegistryEntryResponse>>> GetEntries(string code, CancellationToken cancellationToken)
    {
        var registry = await FindAsync(code, cancellationToken);
        return registry is null ? NotFound(NotFoundProblem()) : Ok(await ToEntriesAsync(registry.id, cancellationToken));
    }

    [HttpPost("{code}/entries")]
    public async Task<ActionResult<PublicRegistryEntryResponse>> CreateEntry(
        string code,
        CreatePublicRegistryEntryRequest request,
        CancellationToken cancellationToken)
    {
        var registry = await FindAsync(code, cancellationToken);
        if (registry is null) return NotFound(NotFoundProblem());
        var errors = ValidateEntry(request);
        if (errors.Count > 0) return ValidationFailure(errors);
        if (await db.PublicRegistryEntries.AnyAsync(item => item.registry_id == registry.id && item.position_number == request.PositionNumber!.Trim(), cancellationToken)) return Conflict(ApiProblemDetails.Create(HttpContext, 409, "Număr duplicat", "Există deja o poziție cu acest număr în registru."));

        var entry = new PublicRegistryEntry
        {
            id = Guid.NewGuid(),
            registry_id = registry.id,
            position_number = request.PositionNumber!.Trim(),
            title = request.Title!.Trim(),
            entry_date = request.EntryDate,
            description = sanitizer.Sanitize(request.Description),
            is_published = false
        };
        db.PublicRegistryEntries.Add(entry);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsUnique(exception)) { return Conflict(ApiProblemDetails.Create(HttpContext, 409, "Număr duplicat", "Există deja o poziție cu acest număr în registru.")); }
        return Created($"/api/public-registries/{registry.code}/entries/{entry.id}", await ToEntryResponseAsync(entry.id, cancellationToken));
    }

    [HttpPut("{code}/entries/{entryId:guid}")]
    public async Task<ActionResult<PublicRegistryEntryResponse>> UpdateEntry(
        string code,
        Guid entryId,
        UpdatePublicRegistryEntryRequest request,
        CancellationToken cancellationToken)
    {
        var registry = await FindAsync(code, cancellationToken);
        var entry = registry is null ? null : await db.PublicRegistryEntries.SingleOrDefaultAsync(item => item.id == entryId && item.registry_id == registry.id, cancellationToken);
        if (registry is null || entry is null) return NotFound(EntryNotFound());
        var errors = ValidateEntry(request);
        if (errors.Count > 0) return ValidationFailure(errors);
        if (request.PositionNumber!.Trim() != entry.position_number && await db.PublicRegistryEntries.AnyAsync(item => item.registry_id == registry.id && item.position_number == request.PositionNumber.Trim() && item.id != entry.id, cancellationToken)) return Conflict(ApiProblemDetails.Create(HttpContext, 409, "Număr duplicat", "Există deja o poziție cu acest număr în registru."));
        entry.position_number = request.PositionNumber.Trim(); entry.title = request.Title!.Trim(); entry.entry_date = request.EntryDate; entry.description = sanitizer.Sanitize(request.Description); entry.is_published = request.IsPublished;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await ToEntryResponseAsync(entry.id, cancellationToken));
    }

    [HttpDelete("{code}/entries/{entryId:guid}")]
    public async Task<IActionResult> DeleteEntry(string code, Guid entryId, CancellationToken cancellationToken)
    {
        var registry = await FindAsync(code, cancellationToken);
        var entry = registry is null ? null : await db.PublicRegistryEntries.SingleOrDefaultAsync(item => item.id == entryId && item.registry_id == registry.id, cancellationToken);
        if (entry is null) return NotFound(EntryNotFound());
        db.PublicRegistryEntries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{code}/entries/{entryId:guid}/publish")]
    public async Task<ActionResult<PublicRegistryEntryResponse>> PublishEntry(string code, Guid entryId, CancellationToken cancellationToken)
    {
        var registry = await FindAsync(code, cancellationToken);
        var entry = registry is null ? null : await db.PublicRegistryEntries.SingleOrDefaultAsync(item => item.id == entryId && item.registry_id == registry.id, cancellationToken);
        if (entry is null) return NotFound(EntryNotFound());
        entry.is_published = true;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await ToEntryResponseAsync(entry.id, cancellationToken));
    }

    [HttpPost("{code}/entries/{entryId:guid}/unpublish")]
    public async Task<ActionResult<PublicRegistryEntryResponse>> UnpublishEntry(string code, Guid entryId, CancellationToken cancellationToken)
    {
        var registry = await FindAsync(code, cancellationToken);
        var entry = registry is null ? null : await db.PublicRegistryEntries.SingleOrDefaultAsync(item => item.id == entryId && item.registry_id == registry.id, cancellationToken);
        if (entry is null) return NotFound(EntryNotFound());
        entry.is_published = false;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await ToEntryResponseAsync(entry.id, cancellationToken));
    }

    [HttpPost("{code}/entries/{entryId:guid}/documents")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<PublicRegistryEntryResponse>> UploadDocument(
        string code,
        Guid entryId,
        [FromForm] UploadPublicRegistryDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var registry = await FindAsync(code, cancellationToken);
        var entry = registry is null ? null : await db.PublicRegistryEntries.SingleOrDefaultAsync(item => item.id == entryId && item.registry_id == registry.id, cancellationToken);
        if (entry is null) return NotFound(EntryNotFound());
        if (request.File is null) return ValidationFailure(new Dictionary<string, string[]> { ["file"] = ["Fișierul este obligatoriu."] });

        StoredSubmissionFile stored;
        try { stored = await storage.StoreAsync(request.File, SubmissionFileStorageService.MaxFileSize, cancellationToken); }
        catch (InvalidSubmissionFileException exception)
        {
            var status = request.File.Length > SubmissionFileStorageService.MaxFileSize ? 413 : 422;
            return StatusCode(status, ApiProblemDetails.Create(HttpContext, status, status == 413 ? "Fișier prea mare" : "Fișier invalid", exception.Message));
        }

        var document = new PublicRegistryDocument
        {
            id = Guid.NewGuid(),
            entry_id = entry.id,
            storage_key = stored.StorageKey,
            original_name = Path.GetFileName(request.File.FileName),
            content_type = stored.ContentType,
            size_bytes = stored.SizeBytes,
            sha256 = stored.Sha256,
            display_order = request.DisplayOrder
        };
        db.PublicRegistryDocuments.Add(document);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch { await storage.DeleteAsync(stored.StorageKey); throw; }
        return Ok(await ToEntryResponseAsync(entry.id, cancellationToken));
    }

    [HttpDelete("{code}/entries/{entryId:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(string code, Guid entryId, Guid documentId, CancellationToken cancellationToken)
    {
        var registry = await FindAsync(code, cancellationToken);
        var document = registry is null ? null : await db.PublicRegistryDocuments.Include(item => item.entry).SingleOrDefaultAsync(item => item.id == documentId && item.entry_id == entryId && item.entry.registry_id == registry.id, cancellationToken);
        if (document is null) return NotFound(ApiProblemDetails.Create(HttpContext, 404, "Document inexistent", "Documentul nu există."));
        db.PublicRegistryDocuments.Remove(document);
        await db.SaveChangesAsync(cancellationToken);
        await storage.DeleteAsync(document.storage_key);
        return NoContent();
    }

    private async Task<PublicRegistry?> FindAsync(string code, CancellationToken cancellationToken) => await db.PublicRegistrations.SingleOrDefaultAsync(item => item.code == code.Trim().ToLowerInvariant(), cancellationToken);

    private async Task<PublicRegistryResponse> ToResponseAsync(PublicRegistry registry, CancellationToken cancellationToken) => new(registry.id, registry.code, registry.name, registry.description, registry.is_published, registry.display_order, await ToEntriesAsync(registry.id, cancellationToken));

    private async Task<IReadOnlyList<PublicRegistryEntryResponse>> ToEntriesAsync(Guid registryId, CancellationToken cancellationToken)
    {
        var entryIds = await db.PublicRegistryEntries.AsNoTracking()
            .Where(item => item.registry_id == registryId)
            .OrderByDescending(item => item.entry_date)
            .ThenBy(item => item.position_number)
            .Select(item => item.id)
            .ToListAsync(cancellationToken);
        var entries = new List<PublicRegistryEntryResponse>(entryIds.Count);
        foreach (var entryId in entryIds)
        {
            entries.Add(await ToEntryResponseAsync(entryId, cancellationToken));
        }
        return entries;
    }

    private async Task<PublicRegistryEntryResponse> ToEntryResponseAsync(Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await db.PublicRegistryEntries.AsNoTracking().SingleAsync(item => item.id == entryId, cancellationToken);
        var documents = await db.PublicRegistryDocuments.AsNoTracking().Where(item => item.entry_id == entryId).OrderBy(item => item.display_order).ThenBy(item => item.original_name).ToListAsync(cancellationToken);
        return new PublicRegistryEntryResponse(entry.id, entry.position_number, entry.title, entry.entry_date, entry.description, entry.is_published, documents.Select(document => ToDocumentResponse(document)).ToList());
    }

    private PublicRegistryDocumentResponse ToDocumentResponse(PublicRegistryDocument document)
    {
        var token = downloadTokens.Create(document.id).Token;
        return new(document.id, document.original_name, document.content_type, document.size_bytes, document.display_order, $"/api/public/registries/documents/{document.id}/download?token={Uri.EscapeDataString(token)}");
    }

    private static Dictionary<string, string[]> ValidateRegistry(string code, string? name, int displayOrder) => new[] { !CodePattern.IsMatch(code) ? new KeyValuePair<string, string[]>("code", ["Codul poate conține litere mici, cifre și cratime."]) : default, string.IsNullOrWhiteSpace(name) ? new KeyValuePair<string, string[]>("name", ["Denumirea este obligatorie."]) : default, displayOrder < 0 ? new KeyValuePair<string, string[]>("displayOrder", ["Ordinea nu poate fi negativă."]) : default }.Where(item => item.Key is not null).ToDictionary(item => item.Key, item => item.Value);

    private static Dictionary<string, string[]> ValidateEntry(CreatePublicRegistryEntryRequest request) => new[] { string.IsNullOrWhiteSpace(request.PositionNumber) ? new KeyValuePair<string, string[]>("positionNumber", ["Numărul poziției este obligatoriu."]) : default, string.IsNullOrWhiteSpace(request.Title) ? new KeyValuePair<string, string[]>("title", ["Titlul este obligatoriu."]) : default }.Where(item => item.Key is not null).ToDictionary(item => item.Key, item => item.Value);

    private ActionResult PageError(int page, int pageSize) => UnprocessableEntity(ApiProblemDetails.CreateValidation(HttpContext, 422, "Paginare invalidă", "Parametrii de paginare sunt invalizi.", new Dictionary<string, string[]> { ["page"] = page < 1 ? ["Pagina trebuie să fie cel puțin 1."] : [], ["pageSize"] = pageSize is < 1 or > 100 ? ["pageSize trebuie să fie între 1 și 100."] : [] }));
    private ActionResult ValidationFailure(Dictionary<string, string[]> errors) => UnprocessableEntity(ApiProblemDetails.CreateValidation(HttpContext, 422, "Date invalide", "Unul sau mai multe câmpuri sunt invalide.", errors));
    private ProblemDetails NotFoundProblem() => ApiProblemDetails.Create(HttpContext, 404, "Registru inexistent", "Registrul public nu există.");
    private ProblemDetails EntryNotFound() => ApiProblemDetails.Create(HttpContext, 404, "Poziție inexistentă", "Poziția nu există în registru.");
    private ProblemDetails DuplicateCode() => ApiProblemDetails.Create(HttpContext, 409, "Cod duplicat", "Există deja un registru public cu acest cod.");
    private static bool IsUnique(DbUpdateException exception) => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
