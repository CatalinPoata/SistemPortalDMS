using API_DMS.Data;
using API_DMS.Entities;
using API_DMS.Entities.Base;
using API_DMS.Errors;
using API_DMS.Integration;
using API_DMS.Reports;
using API_DMS.Services;
using API_DMS.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Npgsql;
using Shared.Reporting;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace API_DMS.Controllers.Integration
{
    [ApiController]
    [Route("api/integration/registry-entries")]
    [ServiceAuthentication]
    public sealed class IntegrationRegistryEntriesController : ControllerBase
    {
        private const string Endpoint =
            "/api/integration/registry-entries";

        private readonly DmsDbContext db;
        private readonly RegistryNumberAllocator numberAllocator;
        private readonly FileStorageService storage;
        private readonly IPortalSubmissionFileClient portalFiles;
        private readonly PortalIntegrationOptions options;
        private readonly DmsReportPreviewService reportPreview;
        private readonly IPdfRenderer pdfRenderer;

        public IntegrationRegistryEntriesController(
            DmsDbContext db,
            RegistryNumberAllocator numberAllocator,
            FileStorageService storage,
            IPortalSubmissionFileClient portalFiles,
            IOptions<PortalIntegrationOptions> options,
            DmsReportPreviewService reportPreview,
            IPdfRenderer pdfRenderer)
        {
            this.db = db;
            this.numberAllocator = numberAllocator;
            this.storage = storage;
            this.portalFiles = portalFiles;
            this.options = options.Value;
            this.reportPreview = reportPreview;
            this.pdfRenderer = pdfRenderer;
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            PortalRegistryEntryRequest request,
            CancellationToken cancellationToken)
        {
            var idempotencyKey = Request.Headers["Idempotency-Key"]
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(idempotencyKey) ||
                idempotencyKey.Length > 100)
            {
                return BadRequest(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status400BadRequest,
                    "Cheie de idempotență invalidă",
                    "Antetul Idempotency-Key este obligatoriu și are maximum 100 caractere."));
            }

            var rawBody = HttpContext.Items[
                ServiceAuthenticationFilter.RawBodyItemKey] as string ?? string.Empty;
            var requestHash = Hash(rawBody);

            var existing = await db.InboundRequests
                .AsNoTracking()
                .SingleOrDefaultAsync(item =>
                    item.endpoint == Endpoint &&
                    item.idempotency_key == idempotencyKey,
                    cancellationToken);

            if (existing is not null)
            {
                if (!FixedTimeEquals(existing.request_hash, requestHash))
                {
                    return Conflict(ApiProblemDetails.Create(
                        HttpContext,
                        StatusCodes.Status409Conflict,
                        "Cheie de idempotență reutilizată",
                        "Aceeași cheie a fost trimisă anterior cu un corp diferit."));
                }

                return JsonResult(
                    existing.response_status,
                    existing.response_body.RootElement.GetRawText());
            }

            try
            {
                await RegisterAsync(
                    request,
                    idempotencyKey,
                    requestHash,
                    cancellationToken);

                var persisted = await db.InboundRequests
                    .AsNoTracking()
                    .SingleAsync(item =>
                        item.endpoint == Endpoint &&
                        item.idempotency_key == idempotencyKey,
                        cancellationToken);

                return JsonResult(
                    persisted.response_status,
                    persisted.response_body.RootElement.GetRawText());
            }
            catch (PortalIntegrationRuleException exception)
            {
                return UnprocessableEntity(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Cerere Portal invalidă",
                    exception.Message));
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException postgres &&
                      postgres.SqlState == "23505")
            {
                var replay = await db.InboundRequests
                    .AsNoTracking()
                    .SingleOrDefaultAsync(item =>
                        item.endpoint == Endpoint &&
                        item.idempotency_key == idempotencyKey,
                        cancellationToken);

                if (replay is not null &&
                    FixedTimeEquals(replay.request_hash, requestHash))
                {
                    return JsonResult(
                        replay.response_status,
                        replay.response_body.RootElement.GetRawText());
                }

                throw;
            }
        }

        [HttpGet("{entryId:guid}/receipt")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        [Produces("application/pdf")]
        public async Task<IActionResult> DownloadReceipt(
            Guid entryId,
            CancellationToken cancellationToken)
        {
            var entryExists = await db.RegistryEntries
                .AsNoTracking()
                .AnyAsync(entry => entry.id == entryId, cancellationToken);
            if (!entryExists)
            {
                return NotFound(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    "Poziție inexistentă",
                    "Poziția de registratură nu există."));
            }

            var report = await db.ReportDefinitions
                .AsNoTracking()
                .SingleOrDefaultAsync(item =>
                    item.code == "dovada-inregistrare",
                    cancellationToken);
            if (report is null)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    ApiProblemDetails.Create(
                        HttpContext,
                        StatusCodes.Status503ServiceUnavailable,
                        "Dovada nu este configurată",
                        "Definiția raportului pentru dovada de înregistrare nu este disponibilă."));
            }

            var query = new QueryCollection(
                new Dictionary<string, StringValues>
                {
                    ["entryId"] = entryId.ToString()
                });

            try
            {
                var preview = await reportPreview.ExportAsync(
                    report,
                    query,
                    cancellationToken);
                var document = DmsReportHtmlDocument.Create(
                    report,
                    preview,
                    query);
                var pdf = await pdfRenderer.RenderAsync(
                    document.Html,
                    new PdfRenderOptions(document.ShowPageNumbers),
                    cancellationToken);

                return File(
                    pdf,
                    "application/pdf",
                    $"dovada-inregistrare-{entryId:N}.pdf");
            }
            catch (ReportPreviewValidationException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    ApiProblemDetails.Create(
                        HttpContext,
                        StatusCodes.Status503ServiceUnavailable,
                        "Dovada nu este configurată",
                        "Definiția raportului pentru dovada de înregistrare este invalidă."));
            }
            catch (TimeoutException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    ApiProblemDetails.Create(
                        HttpContext,
                        StatusCodes.Status503ServiceUnavailable,
                        "Generarea PDF este temporar indisponibilă",
                        "Motorul este ocupat sau randarea a durat prea mult. Reîncearcă."));
            }
        }

        private async Task<PortalRegistryEntryResponse> RegisterAsync(
            PortalRegistryEntryRequest request,
            string idempotencyKey,
            string requestHash,
            CancellationToken cancellationToken)
        {
            ValidateRequest(request);

            var actorEmail = options.ActorEmail.Trim().ToLowerInvariant();
            var actor = await db.Users.SingleOrDefaultAsync(user =>
                user.email == actorEmail &&
                user.is_active &&
                (user.role == Role.Clerk || user.role == Role.Admin),
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "Utilizatorul configurat pentru integrarea Portal nu există sau nu este activ.");

            var registryCode = request.RegistryTypeCode!.Trim().ToUpperInvariant();
            var registry = await db.RegistryTypes.SingleOrDefaultAsync(item =>
                item.code == registryCode,
                cancellationToken)
                ?? throw new PortalIntegrationRuleException(
                    "Registrul configurat pentru serviciu nu există în DMS.");

            if (registry.is_closed)
            {
                throw new PortalIntegrationRuleException(
                    "Registrul configurat pentru serviciu este închis.");
            }

            if (registry.direction is not (RegistryDirection.In or RegistryDirection.Both))
            {
                throw new PortalIntegrationRuleException(
                    "Registrul configurat nu acceptă poziții de intrare.");
            }

            var duplicateExternalId = await db.RegistryEntries.AnyAsync(item =>
                item.external_id == request.ExternalId,
                cancellationToken);

            if (duplicateExternalId)
            {
                throw new PortalIntegrationRuleException(
                    "Cererea Portal a fost deja înregistrată cu altă cheie de idempotență.");
            }

            var kindCodes = request.Documents
                .Select(item => item.DocumentKindCode!.Trim().ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var documentKinds = await db.DocumentKinds
                .Where(item => kindCodes.Contains(item.code) && item.is_active)
                .ToDictionaryAsync(item => item.code, cancellationToken);

            if (documentKinds.Count != kindCodes.Length)
            {
                throw new PortalIntegrationRuleException(
                    "Un tip de document trimis de Portal nu există sau este inactiv în DMS.");
            }

            var storedFiles = new List<StoredFile>();

            try
            {
                await using var transaction = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(
                        cancellationToken)
                    : null;
                var registeredAt = DateTimeOffset.UtcNow;
                var registrationDate = DateOnly.FromDateTime(
                    registeredAt.UtcDateTime);
                var year = registeredAt.UtcDateTime.Year;
                var number = await numberAllocator.AllocateAsync(
                    registry.id,
                    year);

                var entry = new RegistryEntry
                {
                    id = Guid.NewGuid(),
                    external_id = request.ExternalId,
                    registry_type_id = registry.id,
                    year = year,
                    number = number,
                    direction = EntryDirection.In,
                    registered_at = registeredAt,
                    submitted_at = request.SubmittedAt.ToUniversalTime(),
                    subject = request.Subject!.Trim(),
                    applicant_name = request.Applicant!.Name!.Trim(),
                    applicant_national_id = request.Applicant.NationalId?.Trim(),
                    applicant_email = request.Applicant.Email?.Trim(),
                    applicant_phone = request.Applicant.Phone?.Trim(),
                    applicant_address = request.Applicant.Address?.Trim(),
                    service_code = request.ServiceCode!.Trim().ToLowerInvariant(),
                    form_values = JsonDocument.Parse(
                        request.FormValues.GetRawText()),
                    deadline = registrationDate.AddDays(
                        registry.default_deadline_days),
                    status = EntryStatus.Registered,
                    created_by_user_id = actor.id
                };

                db.RegistryEntries.Add(entry);
                db.EntryEvents.Add(new EntryEvent
                {
                    id = Guid.NewGuid(),
                    entry_id = entry.id,
                    occurred_at = registeredAt,
                    actor_user_id = actor.id,
                    type = EventType.Created,
                    message = "Poziția a fost creată din Portal.",
                    payload = JsonSerializer.SerializeToDocument(new
                    {
                        externalId = request.ExternalId,
                        serviceCode = entry.service_code
                    })
                });

                foreach (var document in request.Documents)
                {
                    await using var downloaded = await portalFiles.DownloadAsync(
                        document.FileId,
                        cancellationToken);

                    if (downloaded.ContentLength is > FileStorageService.MaxFileSize)
                    {
                        throw new PortalIntegrationRuleException(
                            "Un fișier trimis de Portal depășește limita de 10 MB.");
                    }

                    var stored = await storage.StoreAsync(
                        downloaded.Stream,
                        cancellationToken);

                    if (stored.SizeBytes != document.SizeBytes ||
                        !string.Equals(
                            stored.ContentType,
                            document.ContentType,
                            StringComparison.OrdinalIgnoreCase) ||
                        !FixedTimeEquals(stored.Sha256, document.Sha256!))
                    {
                        await storage.DeleteAsync(stored.StorageKey);
                        throw new PortalIntegrationRuleException(
                            "Metadatele unui fișier Portal nu corespund conținutului descărcat.");
                    }

                    storedFiles.Add(stored);
                    var documentKind = documentKinds[
                        document.DocumentKindCode!.Trim().ToUpperInvariant()];
                    var registryDocument = new RegistryDocument
                    {
                        id = Guid.NewGuid(),
                        entry_id = entry.id,
                        external_file_id = document.FileId,
                        direction = DocumentDirection.In,
                        document_kind_id = documentKind.id,
                        document_date = registrationDate,
                        issuer = entry.applicant_name,
                        storage_key = stored.StorageKey,
                        original_name = document.Name!.Trim(),
                        content_type = stored.ContentType,
                        size_bytes = stored.SizeBytes,
                        sha256 = stored.Sha256,
                        uploaded_by_user_id = actor.id
                    };

                    db.RegistryDocuments.Add(registryDocument);
                    db.EntryEvents.Add(new EntryEvent
                    {
                        id = Guid.NewGuid(),
                        entry_id = entry.id,
                        occurred_at = registeredAt,
                        actor_user_id = actor.id,
                        type = EventType.DocumentAdded,
                        message = $"A fost adăugat documentul {registryDocument.original_name} din Portal.",
                        payload = JsonSerializer.SerializeToDocument(new
                        {
                            documentId = registryDocument.id,
                            externalFileId = document.FileId
                        })
                    });
                }

                var response = new PortalRegistryEntryResponse(
                    entry.id,
                    registry.code,
                    registry.name,
                    entry.number,
                    entry.year,
                    $"{entry.number}/{entry.year}",
                    entry.registered_at,
                    entry.status.ToString());

                db.InboundRequests.Add(new InboundRequest
                {
                    endpoint = Endpoint,
                    idempotency_key = idempotencyKey,
                    request_hash = requestHash,
                    response_status = StatusCodes.Status201Created,
                    response_body = JsonSerializer.SerializeToDocument(
                        response,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web))
                });

                await db.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return response;
            }
            catch
            {
                foreach (var file in storedFiles)
                {
                    await storage.DeleteAsync(file.StorageKey);
                }

                throw;
            }
        }

        private static void ValidateRequest(PortalRegistryEntryRequest request)
        {
            if (request.ExternalId == Guid.Empty ||
                string.IsNullOrWhiteSpace(request.RegistryTypeCode) ||
                request.RegistryTypeCode.Length > 30 ||
                string.IsNullOrWhiteSpace(request.ServiceCode) ||
                request.ServiceCode.Length > 50 ||
                !string.Equals(request.Direction, "In", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(request.Subject) ||
                request.Subject.Length > 1000 ||
                request.Applicant is null ||
                string.IsNullOrWhiteSpace(request.Applicant.Name) ||
                request.Applicant.Name.Length > 200 ||
                string.IsNullOrWhiteSpace(request.Applicant.Email) ||
                request.Applicant.Email.Length > 256 ||
                request.FormValues.ValueKind != JsonValueKind.Object ||
                request.SubmittedAt == default)
            {
                throw new PortalIntegrationRuleException(
                    "Corpul cererii Portal nu respectă contractul de integrare.");
            }

            if (request.Documents.Any(document =>
                document.FileId == Guid.Empty ||
                string.IsNullOrWhiteSpace(document.Name) ||
                document.Name.Length > 255 ||
                string.IsNullOrWhiteSpace(document.ContentType) ||
                document.ContentType.Length > 120 ||
                document.SizeBytes is <= 0 or > FileStorageService.MaxFileSize ||
                string.IsNullOrWhiteSpace(document.Sha256) ||
                document.Sha256.Length != 64 ||
                string.IsNullOrWhiteSpace(document.DocumentKindCode) ||
                document.DocumentKindCode.Length > 30) ||
                request.Documents.Select(document => document.FileId)
                    .Distinct().Count() != request.Documents.Count)
            {
                throw new PortalIntegrationRuleException(
                    "Metadatele documentelor trimise de Portal sunt invalide.");
            }
        }

        private static string Hash(string value)
        {
            return Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        }

        private static bool FixedTimeEquals(string first, string second)
        {
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(first),
                Encoding.UTF8.GetBytes(second));
        }

        private static ContentResult JsonResult(int statusCode, string body)
        {
            return new ContentResult
            {
                StatusCode = statusCode,
                ContentType = "application/json; charset=utf-8",
                Content = body
            };
        }
    }
}
