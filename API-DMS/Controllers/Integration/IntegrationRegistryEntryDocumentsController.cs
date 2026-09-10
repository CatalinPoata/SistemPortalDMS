using API_DMS.Data;
using API_DMS.Entities;
using API_DMS.Entities.Base;
using API_DMS.Errors;
using API_DMS.Integration;
using API_DMS.Services;
using API_DMS.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace API_DMS.Controllers.Integration
{
    [ApiController]
    [Route("api/integration/registry-entries")]
    [ServiceAuthentication]
    public sealed class IntegrationRegistryEntryDocumentsController
        : ControllerBase
    {
        private const string Endpoint =
            "/api/integration/registry-entries/{entryId}/documents";

        private readonly DmsDbContext db;
        private readonly RegistryEntryWorkflowService workflow;
        private readonly FileStorageService storage;
        private readonly IPortalSubmissionFileClient portalFiles;
        private readonly PortalIntegrationOptions options;
        private readonly ILogger<IntegrationRegistryEntryDocumentsController>
            logger;

        public IntegrationRegistryEntryDocumentsController(
            DmsDbContext db,
            RegistryEntryWorkflowService workflow,
            FileStorageService storage,
            IPortalSubmissionFileClient portalFiles,
            IOptions<PortalIntegrationOptions> options,
            ILogger<IntegrationRegistryEntryDocumentsController> logger)
        {
            this.db = db;
            this.workflow = workflow;
            this.storage = storage;
            this.portalFiles = portalFiles;
            this.options = options.Value;
            this.logger = logger;
        }

        [HttpPost("{entryId:guid}/documents")]
        public async Task<IActionResult> AddDocuments(
            Guid entryId,
            PortalClarificationRequest request,
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
                var response = await AddDocumentsAsync(
                    entryId,
                    request,
                    idempotencyKey,
                    requestHash,
                    cancellationToken);

                return JsonResult(
                    StatusCodes.Status200OK,
                    JsonSerializer.Serialize(
                        response,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            }
            catch (PortalIntegrationRuleException exception)
            {
                return UnprocessableEntity(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Completare Portal invalidă",
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
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Eroare la preluarea completărilor Portal pentru poziția {EntryId}.",
                    entryId);
                throw;
            }
        }

        private async Task<PortalClarificationResponse> AddDocumentsAsync(
            Guid entryId,
            PortalClarificationRequest request,
            string idempotencyKey,
            string requestHash,
            CancellationToken cancellationToken)
        {
            ValidateRequest(entryId, request);

            var actorEmail = options.ActorEmail.Trim().ToLowerInvariant();
            var actor = await db.Users.SingleOrDefaultAsync(user =>
                user.email == actorEmail &&
                user.is_active &&
                (user.role == Role.Clerk || user.role == Role.Admin),
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "Utilizatorul configurat pentru integrarea Portal nu există sau nu este activ.");

            var entry = await db.RegistryEntries.SingleOrDefaultAsync(item =>
                item.id == entryId && item.external_id == request.ExternalId,
                cancellationToken)
                ?? throw new PortalIntegrationRuleException(
                    "Poziția DMS nu corespunde cererii Portal.");

            if (entry.status != EntryStatus.InfoRequested)
            {
                throw new PortalIntegrationRuleException(
                    "Poziția nu așteaptă clarificări.");
            }

            var existingFile = await db.RegistryDocuments.AnyAsync(item =>
                request.Documents.Select(document => document.FileId)
                    .Contains(item.external_file_id ?? Guid.Empty),
                cancellationToken);
            if (existingFile)
            {
                throw new PortalIntegrationRuleException(
                    "Un document de clarificare a fost deja primit.");
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
                    ? await db.Database.BeginTransactionAsync(cancellationToken)
                    : null;
                var occurredAt = DateTimeOffset.UtcNow;
                var documentDate = DateOnly.FromDateTime(occurredAt.UtcDateTime);
                var documentIds = new List<Guid>();

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
                    var registryDocument = new RegistryDocument
                    {
                        id = Guid.NewGuid(),
                        entry_id = entry.id,
                        external_file_id = document.FileId,
                        direction = DocumentDirection.In,
                        document_kind_id = documentKinds[
                            document.DocumentKindCode!.Trim().ToUpperInvariant()].id,
                        document_date = documentDate,
                        issuer = "Portal - clarificare cetățean",
                        storage_key = stored.StorageKey,
                        original_name = document.Name!.Trim(),
                        content_type = stored.ContentType,
                        size_bytes = stored.SizeBytes,
                        sha256 = stored.Sha256,
                        uploaded_by_user_id = actor.id
                    };
                    documentIds.Add(registryDocument.id);
                    db.RegistryDocuments.Add(registryDocument);
                    db.EntryEvents.Add(new EntryEvent
                    {
                        id = Guid.NewGuid(),
                        entry_id = entry.id,
                        occurred_at = occurredAt,
                        actor_user_id = actor.id,
                        type = EventType.DocumentAdded,
                        message = "A fost adăugat un document de clarificare din Portal.",
                        payload = JsonSerializer.SerializeToDocument(new
                        {
                            documentId = registryDocument.id,
                            externalFileId = document.FileId
                        })
                    });
                }

                await db.SaveChangesAsync(cancellationToken);
                var transition = await workflow.TransitionAsync(
                    entry.id,
                    EntryStatus.InReview,
                    null,
                    actor.id,
                    cancellationToken);
                if (transition is null)
                {
                    throw new PortalIntegrationRuleException(
                        "Poziția DMS nu mai există.");
                }

                var response = new PortalClarificationResponse(
                    entry.id,
                    EntryStatus.InReview.ToString(),
                    documentIds);
                db.InboundRequests.Add(new InboundRequest
                {
                    endpoint = Endpoint,
                    idempotency_key = idempotencyKey,
                    request_hash = requestHash,
                    response_status = StatusCodes.Status200OK,
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

        private static void ValidateRequest(
            Guid entryId,
            PortalClarificationRequest request)
        {
            if (entryId == Guid.Empty ||
                request.EntryId != entryId ||
                request.ExternalId == Guid.Empty ||
                request.Documents.Count == 0 ||
                request.Documents.Any(document =>
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
                    "Corpul completării Portal nu respectă contractul de integrare.");
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
