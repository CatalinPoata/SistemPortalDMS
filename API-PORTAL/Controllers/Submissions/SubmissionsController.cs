using API_PORTAL.Data;
using API_PORTAL.DTO.ServiceDefinitions;
using API_PORTAL.DTO.Submissions;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using API_PORTAL.Errors;
using API_PORTAL.Integration;
using API_PORTAL.Services;
using API_PORTAL.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace API_PORTAL.Controllers.Submissions
{
    [ApiController]
    [Route("api/submissions")]
    [Authorize(Roles = nameof(Role.Citizen))]
    public sealed class SubmissionsController : ControllerBase
    {
        public const string GeneralAttachmentKey = "__attachment__";

        private static readonly HashSet<SubmissionStatus>
            CitizenWithdrawalStatuses =
        [
            SubmissionStatus.Submitted,
            SubmissionStatus.Registered,
            SubmissionStatus.InfoRequested
        ];

        private readonly PortalDbContext db;
        private readonly ServiceFormValueValidator valueValidator;
        private readonly SubmissionFileStorageService storage;
        private readonly SubmissionFileDownloadTokenService downloadTokens;
        private readonly IDmsResponseDocumentClient responseDocuments;
        private readonly IDmsRegistrationReceiptClient registrationReceipts;

        public SubmissionsController(
            PortalDbContext db,
            ServiceFormValueValidator valueValidator,
            SubmissionFileStorageService storage,
            SubmissionFileDownloadTokenService downloadTokens,
            IDmsResponseDocumentClient responseDocuments,
            IDmsRegistrationReceiptClient registrationReceipts)
        {
            this.db = db;
            this.valueValidator = valueValidator;
            this.storage = storage;
            this.downloadTokens = downloadTokens;
            this.responseDocuments = responseDocuments;
            this.registrationReceipts = registrationReceipts;
        }

        [HttpPost]
        [Consumes("application/json")]
        public async Task<ActionResult<SubmissionDetailsResponse>> Create(
            CreateSubmissionRequest request,
            CancellationToken cancellationToken)
        {
            var forbiddenFields = RejectAdditionalProperties(
                request.AdditionalProperties);

            if (forbiddenFields is not null)
            {
                return forbiddenFields;
            }

            if (!TryGetUserId(out var userId))
            {
                return Forbid();
            }

            var user = await db.Users.SingleOrDefaultAsync(
                item => item.id == userId,
                cancellationToken);

            if (user is null || !user.is_active)
            {
                return Forbid();
            }

            if (!user.email_confirmed)
            {
                return EmailConfirmationRequired();
            }

            var serviceCode = request.ServiceCode?
                .Trim()
                .ToLowerInvariant() ?? string.Empty;
            var service = await db.ServiceDefinitions
                .SingleOrDefaultAsync(
                    item => item.code == serviceCode &&
                        item.is_published,
                    cancellationToken);

            if (service is null)
            {
                return NotFound(ServiceNotFound());
            }

            var values = request.Values!.Value;
            var errors = valueValidator.Validate(
                service.form_schema.RootElement,
                values);

            if (errors.Count > 0)
            {
                return ValuesValidationFailure(errors);
            }

            if (RequiresFileTransfer(service))
            {
                return ValuesValidationFailure(
                    new Dictionary<string, string[]>
                    {
                        ["files"] =
                        [
                            "Acest serviciu necesită încărcarea de fișiere. " +
                            "Folosește fluxul multipart care va fi activat în pasul următor."
                        ]
                    });
            }

            var now = DateTimeOffset.UtcNow;
            var submission = new Submission
            {
                id = Guid.NewGuid(),
                external_id = Guid.NewGuid(),
                service_id = service.id,
                schema_version = service.schema_version,
                form_snapshot = JsonDocument.Parse(
                    service.form_schema.RootElement.GetRawText()),
                user_id = userId,
                values = JsonDocument.Parse(values.GetRawText()),
                status = SubmissionStatus.Submitted,
                submitted_at = now
            };

            var submissionEvent = new SubmissionEvent
            {
                id = Guid.NewGuid(),
                submission_id = submission.id,
                occurred_at = now,
                type = SubmissionEventType.Submitted,
                message = "Cererea a fost depusă."
            };

            db.Submissions.Add(submission);
            db.SubmissionEvents.Add(submissionEvent);
            EnqueueRegistration(submission, service, user, []);
            await db.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = submission.id },
                await ToDetailsAsync(
                    submission,
                    service,
                    cancellationToken));
        }

        [HttpPost("with-files")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(
            SubmissionFileStorageService.MaxSubmissionRequestSize)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status413PayloadTooLarge)]
        public async Task<ActionResult<SubmissionDetailsResponse>>
            CreateWithFiles(
                [FromForm] CreateSubmissionMultipartRequest request,
                CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Forbid();
            }

            var user = await db.Users.SingleOrDefaultAsync(
                item => item.id == userId,
                cancellationToken);

            if (user is null || !user.is_active)
            {
                return Forbid();
            }

            if (!user.email_confirmed)
            {
                return EmailConfirmationRequired();
            }

            var serviceCode = request.ServiceCode?
                .Trim()
                .ToLowerInvariant() ?? string.Empty;
            var service = await db.ServiceDefinitions
                .SingleOrDefaultAsync(
                    item => item.code == serviceCode &&
                        item.is_published,
                    cancellationToken);

            if (service is null)
            {
                return NotFound(ServiceNotFound());
            }

            if (request.Files.Count != request.FileKeys.Count)
            {
                return ValuesValidationFailure(
                    new Dictionary<string, string[]>
                    {
                        ["files"] =
                        [
                            "Fiecare fișier trebuie să aibă o cheie asociată."
                        ]
                    });
            }

            JsonObject valuesNode;

            try
            {
                valuesNode = JsonNode.Parse(request.Values!) as JsonObject
                    ?? throw new JsonException(
                        "Values trebuie să fie un obiect JSON.");
            }
            catch (JsonException)
            {
                return BadRequest(ApiProblemDetails.CreateValidation(
                    HttpContext,
                    StatusCodes.Status400BadRequest,
                    "JSON invalid",
                    "Câmpul Values nu poate fi interpretat ca obiect JSON.",
                    new Dictionary<string, string[]>
                    {
                        ["values"] =
                        [
                            "Values trebuie să conțină un obiect JSON valid."
                        ]
                    }));
            }

            using var originalValues = JsonDocument.Parse(
                valuesNode.ToJsonString());
            var schema = service.form_schema.RootElement;
            var fileFields = ReadFileFields(schema);
            var errors = new Dictionary<string, List<string>>();
            var pending = new List<PendingSubmissionFile>();
            var usedFieldKeys = new HashSet<string>(StringComparer.Ordinal);
            var generalAttachmentCount = 0;

            for (var index = 0; index < request.Files.Count; index++)
            {
                var upload = request.Files[index];
                var requestedKey = request.FileKeys[index].Trim();
                var errorKey = $"files[{index}]";

                if (upload.Length > SubmissionFileStorageService.MaxFileSize)
                {
                    return FileTooLarge();
                }

                var originalName = NormalizeFileName(upload.FileName);

                if (originalName is null)
                {
                    AddError(
                        errors,
                        errorKey,
                        "Numele fișierului este invalid.");
                    continue;
                }

                if (requestedKey == GeneralAttachmentKey)
                {
                    generalAttachmentCount++;
                    pending.Add(new PendingSubmissionFile(
                        Guid.NewGuid(),
                        upload,
                        null,
                        originalName,
                        SubmissionFileStorageService.MaxFileSize,
                        null));
                    continue;
                }

                if (!fileFields.TryGetValue(
                    requestedKey,
                    out var field))
                {
                    AddError(
                        errors,
                        errorKey,
                        "Cheia nu corespunde unui câmp file din schemă.");
                    continue;
                }

                if (!IsVisible(field, originalValues.RootElement))
                {
                    AddError(
                        errors,
                        requestedKey,
                        "Nu se poate trimite un fișier pentru un câmp ascuns.");
                    continue;
                }

                if (!usedFieldKeys.Add(requestedKey))
                {
                    AddError(
                        errors,
                        requestedKey,
                        "Câmpul acceptă un singur fișier.");
                    continue;
                }

                if (originalValues.RootElement.TryGetProperty(
                    requestedKey,
                    out _))
                {
                    AddError(
                        errors,
                        requestedKey,
                        "Referința fileId este generată de server și nu trebuie trimisă în Values.");
                    continue;
                }

                var maximumSize = ReadMaximumFileSize(field);

                if (upload.Length > maximumSize)
                {
                    AddError(
                        errors,
                        requestedKey,
                        $"Fișierul depășește limita de " +
                        $"{maximumSize / (1024 * 1024)} MB a câmpului.");
                    continue;
                }

                var fileId = Guid.NewGuid();
                valuesNode[requestedKey] = new JsonObject
                {
                    ["fileId"] = fileId.ToString()
                };

                pending.Add(new PendingSubmissionFile(
                    fileId,
                    upload,
                    requestedKey,
                    originalName,
                    maximumSize,
                    field));
            }

            if (generalAttachmentCount > service.max_attachments)
            {
                AddError(
                    errors,
                    "attachments",
                    $"Sunt permise cel mult {service.max_attachments} atașamente generale.");
            }

            if (service.requires_attachment && generalAttachmentCount == 0)
            {
                AddError(
                    errors,
                    "attachments",
                    "Este necesar cel puțin un atașament general.");
            }

            using var completeValues = JsonDocument.Parse(
                valuesNode.ToJsonString());
            MergeErrors(
                errors,
                valueValidator.Validate(
                    schema,
                    completeValues.RootElement));

            if (errors.Count > 0)
            {
                return ValuesValidationFailure(ToReadOnly(errors));
            }

            var storedUploads = new List<StoredUpload>();

            foreach (var item in pending)
            {
                StoredSubmissionFile stored;

                try
                {
                    stored = await storage.StoreAsync(
                        item.Upload,
                        item.MaximumSize,
                        cancellationToken);
                }
                catch (InvalidSubmissionFileException exception)
                {
                    await DeleteStoredAsync(storedUploads);

                    return ValuesValidationFailure(
                        new Dictionary<string, string[]>
                        {
                            [item.FieldKey ?? "attachments"] =
                                [exception.Message]
                        });
                }

                if (item.FieldSchema.HasValue &&
                    !AcceptsContentType(
                        item.FieldSchema.Value,
                        stored.ContentType))
                {
                    await storage.DeleteAsync(stored.StorageKey);
                    await DeleteStoredAsync(storedUploads);

                    return ValuesValidationFailure(
                        new Dictionary<string, string[]>
                        {
                            [item.FieldKey!] =
                            [
                                "Tipul real al fișierului nu este permis pentru acest câmp."
                            ]
                        });
                }

                storedUploads.Add(new StoredUpload(item, stored));
            }

            var now = DateTimeOffset.UtcNow;
            var submission = new Submission
            {
                id = Guid.NewGuid(),
                external_id = Guid.NewGuid(),
                service_id = service.id,
                schema_version = service.schema_version,
                form_snapshot = JsonDocument.Parse(schema.GetRawText()),
                user_id = userId,
                values = JsonDocument.Parse(
                    completeValues.RootElement.GetRawText()),
                status = SubmissionStatus.Submitted,
                submitted_at = now
            };

            var files = storedUploads.Select(item => new SubmissionFile
            {
                id = item.Pending.Id,
                submission_id = submission.id,
                field_key = item.Pending.FieldKey,
                kind = SubmissionFileKind.Attachment,
                storage_key = item.Stored.StorageKey,
                original_name = item.Pending.OriginalName,
                content_type = item.Stored.ContentType,
                size_bytes = item.Stored.SizeBytes,
                sha256 = item.Stored.Sha256
            }).ToList();

            db.Submissions.Add(submission);
            db.SubmissionFiles.AddRange(files);
            db.SubmissionEvents.Add(new SubmissionEvent
            {
                id = Guid.NewGuid(),
                submission_id = submission.id,
                occurred_at = now,
                type = SubmissionEventType.Submitted,
                message = "Cererea a fost depusă."
            });
            db.SubmissionEvents.AddRange(files.Select(file =>
                new SubmissionEvent
                {
                    id = Guid.NewGuid(),
                    submission_id = submission.id,
                    occurred_at = now,
                    type = SubmissionEventType.FileAdded,
                    message = $"A fost adăugat fișierul {file.original_name}.",
                    file_id = file.id
                }));
            EnqueueRegistration(submission, service, user, files);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                await DeleteStoredAsync(storedUploads);
                throw;
            }

            return CreatedAtAction(
                nameof(GetById),
                new { id = submission.id },
                await ToDetailsAsync(
                    submission,
                    service,
                    cancellationToken));
        }

        [HttpPost("{id:guid}/clarifications")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(
            SubmissionFileStorageService.MaxSubmissionRequestSize)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status413PayloadTooLarge)]
        public async Task<ActionResult<SubmissionDetailsResponse>>
            UploadClarifications(
                Guid id,
                [FromForm] UploadClarificationsRequest request,
                CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Forbid();
            }

            var submission = await db.Submissions
                .Include(item => item.service)
                .SingleOrDefaultAsync(item => item.id == id,
                    cancellationToken);

            if (submission is null)
            {
                return NotFound(SubmissionNotFound());
            }

            if (submission.user_id != userId)
            {
                return Forbid();
            }

            if (submission.status != SubmissionStatus.InfoRequested ||
                !submission.dms_entry_id.HasValue)
            {
                return UnprocessableEntity(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Completare nepermisă",
                    "Se pot încărca clarificări numai pentru o cerere " +
                    "aflată în starea InfoRequested."));
            }

            if (request.Files.Count == 0)
            {
                return ValuesValidationFailure(
                    new Dictionary<string, string[]>
                    {
                        ["files"] = ["Este necesar cel puțin un fișier."]
                    });
            }

            var storedFiles = new List<StoredSubmissionFile>();
            var files = new List<SubmissionFile>();

            try
            {
                foreach (var upload in request.Files)
                {
                    if (upload.Length > SubmissionFileStorageService.MaxFileSize)
                    {
                        await DeleteStoredFilesAsync(storedFiles);
                        return FileTooLarge();
                    }

                    var originalName = NormalizeFileName(upload.FileName);
                    if (originalName is null)
                    {
                        await DeleteStoredFilesAsync(storedFiles);
                        return ValuesValidationFailure(
                            new Dictionary<string, string[]>
                            {
                                ["files"] = ["Numele fișierului este invalid."]
                            });
                    }

                    StoredSubmissionFile stored;
                    try
                    {
                        stored = await storage.StoreAsync(
                            upload,
                            SubmissionFileStorageService.MaxFileSize,
                            cancellationToken);
                    }
                    catch (InvalidSubmissionFileException exception)
                    {
                        await DeleteStoredFilesAsync(storedFiles);
                        return ValuesValidationFailure(
                            new Dictionary<string, string[]>
                            {
                                ["files"] = [exception.Message]
                            });
                    }

                    storedFiles.Add(stored);
                    files.Add(new SubmissionFile
                    {
                        id = Guid.NewGuid(),
                        submission_id = submission.id,
                        kind = SubmissionFileKind.Attachment,
                        storage_key = stored.StorageKey,
                        original_name = originalName,
                        content_type = stored.ContentType,
                        size_bytes = stored.SizeBytes,
                        sha256 = stored.Sha256
                    });
                }

                var now = DateTimeOffset.UtcNow;
                db.SubmissionFiles.AddRange(files);
                db.SubmissionEvents.AddRange(files.Select(file =>
                    new SubmissionEvent
                    {
                        id = Guid.NewGuid(),
                        submission_id = submission.id,
                        occurred_at = now,
                        type = SubmissionEventType.FileAdded,
                        message = "A fost încărcat un document de clarificare: " +
                            file.original_name,
                        file_id = file.id
                    }));
                db.OutboxMessages.Add(new OutboxMessage
                {
                    id = Guid.NewGuid(),
                    aggregate_type = "Submission",
                    aggregate_id = submission.id,
                    event_type = "Submission.AddDocuments",
                    payload = JsonSerializer.SerializeToDocument(
                        new PortalSubmissionClarificationPayload(
                            submission.external_id,
                            submission.dms_entry_id.Value,
                            files.Select(file => new PortalSubmissionDocument(
                                file.id,
                                file.original_name,
                                file.content_type,
                                file.size_bytes,
                                file.sha256,
                                "ANEXA")).ToArray()),
                        new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                    status = OutboxMessageStatus.Pending,
                    attempts = 0,
                    next_attempt_at = now
                });

                await db.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                await DeleteStoredFilesAsync(storedFiles);
                throw;
            }

            return Ok(await ToDetailsAsync(
                submission,
                submission.service,
                cancellationToken));
        }

        [HttpGet]
        public async Task<ActionResult<
            PagedResponse<SubmissionListItemResponse>>> GetMine(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetUserId(out var userId))
            {
                return Forbid();
            }

            if (page < 1 || pageSize is < 1 or > 100)
            {
                return InvalidPage(page, pageSize);
            }

            var query = db.Submissions
                .AsNoTracking()
                .Where(submission => submission.user_id == userId);

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(submission => submission.submitted_at)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(submission => new SubmissionListItemResponse(
                    submission.id,
                    submission.external_id,
                    submission.service.code,
                    submission.service.title,
                    submission.status,
                    submission.submitted_at,
                    submission.registry_display_number))
                .ToListAsync(cancellationToken);

            return Ok(new PagedResponse<SubmissionListItemResponse>(
                items,
                page,
                pageSize,
                total));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<SubmissionDetailsResponse>> GetById(
            Guid id,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Forbid();
            }

            var submission = await db.Submissions
                .AsNoTracking()
                .Include(item => item.service)
                .SingleOrDefaultAsync(
                    item => item.id == id,
                    cancellationToken);

            if (submission is null)
            {
                return NotFound(SubmissionNotFound());
            }

            if (submission.user_id != userId)
            {
                return Forbid();
            }

            return Ok(await ToDetailsAsync(
                submission,
                submission.service,
                cancellationToken));
        }

        [HttpGet("{id:guid}/registration-receipt")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        [Produces("application/pdf")]
        public async Task<IActionResult> DownloadRegistrationReceipt(
            Guid id,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Forbid();
            }

            var submission = await db.Submissions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.id == id, cancellationToken);
            if (submission is null)
            {
                return NotFound(SubmissionNotFound());
            }

            if (submission.user_id != userId)
            {
                return Forbid();
            }

            if (submission.dms_entry_id is null ||
                submission.registered_at is null)
            {
                return UnprocessableEntity(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Dovada nu este disponibilă",
                    "Dovada poate fi descărcată după înregistrarea cererii în DMS."));
            }

            var receipt = await registrationReceipts.DownloadAsync(
                submission.dms_entry_id.Value,
                cancellationToken);

            if (receipt.Status == DmsReceiptDownloadStatus.NotFound)
            {
                return NotFound(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    "Dovada inexistentă",
                    "Dovada de înregistrare nu mai este disponibilă în DMS."));
            }

            if (receipt.Status != DmsReceiptDownloadStatus.Found ||
                receipt.Content is null)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    ApiProblemDetails.Create(
                        HttpContext,
                        StatusCodes.Status503ServiceUnavailable,
                        "DMS indisponibil",
                        "Dovada de înregistrare nu poate fi obținută acum."));
            }

            var fileName = submission.registry_number.HasValue &&
                submission.registry_year.HasValue
                    ? $"dovada-inregistrare-{submission.registry_number}-" +
                        $"{submission.registry_year}.pdf"
                    : $"dovada-inregistrare-{submission.id:N}.pdf";

            return File(receipt.Content, "application/pdf", fileName);
        }

        [HttpPost("{id:guid}/withdraw")]
        public async Task<ActionResult<SubmissionDetailsResponse>> Withdraw(
            Guid id,
            WithdrawSubmissionRequest request,
            CancellationToken cancellationToken)
        {
            var forbiddenFields = RejectAdditionalProperties(
                request.AdditionalProperties);

            if (forbiddenFields is not null)
            {
                return forbiddenFields;
            }

            if (!TryGetUserId(out var userId))
            {
                return Forbid();
            }

            var submission = await db.Submissions
                .Include(item => item.service)
                .SingleOrDefaultAsync(
                    item => item.id == id,
                    cancellationToken);

            if (submission is null)
            {
                return NotFound(SubmissionNotFound());
            }

            if (submission.user_id != userId)
            {
                return Forbid();
            }

            if (!CitizenWithdrawalStatuses.Contains(submission.status))
            {
                return UnprocessableEntity(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Retragere nepermisă",
                    "Cererea nu mai poate fi retrasă în starea curentă."));
            }

            var reason = request.Reason!.Trim();
            var now = DateTimeOffset.UtcNow;

            if (submission.status is SubmissionStatus.Registered or
                SubmissionStatus.InfoRequested)
            {
                var cancellationAlreadyRequested = await db.OutboxMessages
                    .AnyAsync(item =>
                        item.aggregate_id == submission.id &&
                        item.event_type == "Submission.Cancel",
                        cancellationToken);
                if (cancellationAlreadyRequested)
                {
                    return Conflict(ApiProblemDetails.Create(
                        HttpContext,
                        StatusCodes.Status409Conflict,
                        "Retragere deja în curs",
                        "Cererea de retragere a fost deja transmisă spre DMS."));
                }

                db.OutboxMessages.Add(new OutboxMessage
                {
                    id = Guid.NewGuid(),
                    aggregate_type = "Submission",
                    aggregate_id = submission.id,
                    event_type = "Submission.Cancel",
                    payload = JsonSerializer.SerializeToDocument(
                        new PortalSubmissionCancellationPayload(
                            submission.external_id,
                            submission.dms_entry_id ?? throw new InvalidOperationException(
                                "O cerere înregistrată trebuie să aibă poziție DMS."),
                            reason),
                        new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                    status = OutboxMessageStatus.Pending,
                    attempts = 0,
                    next_attempt_at = now
                });
                submission.status_details =
                    "Retragerea a fost transmisă spre anulare în DMS.";
                db.SubmissionEvents.Add(new SubmissionEvent
                {
                    id = Guid.NewGuid(),
                    submission_id = submission.id,
                    occurred_at = now,
                    type = SubmissionEventType.StatusChanged,
                    message = "Solicitarea de retragere a fost transmisă spre DMS: " +
                        reason
                });

                await db.SaveChangesAsync(cancellationToken);

                return Ok(await ToDetailsAsync(
                    submission,
                    submission.service,
                    cancellationToken));
            }

            submission.status = SubmissionStatus.Cancelled;
            submission.status_details = reason;

            var pendingRegistration = await db.OutboxMessages
                .Where(item => item.aggregate_id == submission.id &&
                    item.event_type == "Submission.Register" &&
                    item.status == OutboxMessageStatus.Pending)
                .ToListAsync(cancellationToken);
            foreach (var message in pendingRegistration)
            {
                message.status = OutboxMessageStatus.Failed;
                message.last_error =
                    "Cererea a fost retrasă înainte de înregistrarea în DMS.";
            }

            db.SubmissionEvents.Add(new SubmissionEvent
            {
                id = Guid.NewGuid(),
                submission_id = submission.id,
                occurred_at = now,
                type = SubmissionEventType.Cancelled,
                message = $"Cererea a fost retrasă: {reason}"
            });

            await db.SaveChangesAsync(cancellationToken);

            return Ok(await ToDetailsAsync(
                submission,
                submission.service,
                cancellationToken));
        }

        [HttpPost("{submissionId:guid}/files/{fileId:guid}/download-url")]
        public async Task<ActionResult<SubmissionFileDownloadUrlResponse>>
            CreateFileDownloadUrl(
                Guid submissionId,
                Guid fileId,
                CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Forbid();
            }

            var file = await db.SubmissionFiles
                .AsNoTracking()
                .Include(item => item.submission)
                .SingleOrDefaultAsync(
                    item => item.id == fileId &&
                        item.submission_id == submissionId,
                    cancellationToken);

            if (file is null)
            {
                return NotFound(FileNotFoundProblem());
            }

            if (file.submission.user_id != userId)
            {
                return Forbid();
            }

            if (file.kind == SubmissionFileKind.Response)
            {
                var result = await responseDocuments.CreateDownloadUrlAsync(
                    file.id,
                    cancellationToken);

                if (result.Status == DmsResponseDocumentLookupStatus.NotFound)
                {
                    return NotFound(ApiProblemDetails.Create(
                        HttpContext,
                        StatusCodes.Status404NotFound,
                        "Document inexistent",
                        "Documentul de răspuns nu mai este disponibil în DMS."));
                }

                if (result.Status != DmsResponseDocumentLookupStatus.Found ||
                    result.Download is null)
                {
                    return StatusCode(
                        StatusCodes.Status503ServiceUnavailable,
                        ApiProblemDetails.Create(
                            HttpContext,
                            StatusCodes.Status503ServiceUnavailable,
                            "DMS indisponibil",
                            "URL-ul documentului de răspuns nu poate fi obținut acum."));
                }

                return Ok(new SubmissionFileDownloadUrlResponse(
                    file.id,
                    result.Download.Url,
                    result.Download.ExpiresAt));
            }

            var signed = downloadTokens.Create(fileId);
            var url =
                $"{Request.Scheme}://{Request.Host}" +
                $"/api/submission-files/{fileId}/content" +
                $"?token={Uri.EscapeDataString(signed.Token)}";

            return Ok(new SubmissionFileDownloadUrlResponse(
                fileId,
                url,
                signed.ExpiresAt));
        }

        [AllowAnonymous]
        [HttpGet("/api/submission-files/{fileId:guid}/content")]
        public async Task<IActionResult> DownloadFile(
            Guid fileId,
            [FromQuery] string? token,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(token) ||
                !downloadTokens.TryValidate(token, fileId, out _))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiProblemDetails.Create(
                        HttpContext,
                        StatusCodes.Status403Forbidden,
                        "URL invalid sau expirat",
                        "URL-ul de descărcare nu mai este valid."));
            }

            var file = await db.SubmissionFiles
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.id == fileId,
                    cancellationToken);

            if (file is null)
            {
                return NotFound(FileNotFoundProblem());
            }

            if (file.kind == SubmissionFileKind.Response)
            {
                return UnprocessableEntity(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Document gestionat de DMS",
                    "Conținutul documentului nu este stocat în Portal."));
            }

            try
            {
                return File(
                    storage.OpenRead(file.storage_key),
                    file.content_type,
                    file.original_name,
                    enableRangeProcessing: true);
            }
            catch (FileNotFoundException)
            {
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Fișier indisponibil",
                    detail: "Metadatele există, dar fișierul lipsește din storage.");
            }
        }

        private async Task<SubmissionDetailsResponse> ToDetailsAsync(
            Submission submission,
            ServiceDefinition service,
            CancellationToken cancellationToken)
        {
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

            return new SubmissionDetailsResponse(
                submission.id,
                submission.external_id,
                service.code,
                service.title,
                submission.schema_version,
                submission.form_snapshot.RootElement.Clone(),
                submission.values.RootElement.Clone(),
                submission.status,
                submission.status_details,
                submission.submitted_at,
                submission.registry_number,
                submission.registry_year,
                submission.registry_display_number,
                submission.registered_at,
                files,
                events);
        }

        private void EnqueueRegistration(
            Submission submission,
            ServiceDefinition service,
            User user,
            IEnumerable<SubmissionFile> files)
        {
            var payload = PortalSubmissionRegistrationPayload.Create(
                submission,
                service,
                user,
                files);

            db.OutboxMessages.Add(new OutboxMessage
            {
                id = Guid.NewGuid(),
                aggregate_type = "Submission",
                aggregate_id = submission.id,
                event_type = "Submission.Register",
                payload = JsonSerializer.SerializeToDocument(
                    payload,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                status = OutboxMessageStatus.Pending,
                attempts = 0,
                next_attempt_at = DateTimeOffset.UtcNow
            });
        }

        private bool TryGetUserId(out Guid userId)
        {
            return Guid.TryParse(
                User.FindFirstValue(JwtRegisteredClaimNames.Sub),
                out userId);
        }

        private bool RequiresFileTransfer(ServiceDefinition service)
        {
            if (service.requires_attachment)
            {
                return true;
            }

            var schema = service.form_schema.RootElement;

            return schema.GetProperty("sections")
                .EnumerateArray()
                .SelectMany(section =>
                    section.GetProperty("fields").EnumerateArray())
                .Any(field =>
                    field.GetProperty("type").GetString() == "file");
        }

        private static Dictionary<string, JsonElement> ReadFileFields(
            JsonElement schema)
        {
            return schema.GetProperty("sections")
                .EnumerateArray()
                .SelectMany(section =>
                    section.GetProperty("fields").EnumerateArray())
                .Where(field =>
                    field.GetProperty("type").GetString() == "file")
                .ToDictionary(
                    field => field.GetProperty("key").GetString()!,
                    field => field,
                    StringComparer.Ordinal);
        }

        private static bool IsVisible(
            JsonElement field,
            JsonElement values)
        {
            if (!field.TryGetProperty("visibleWhen", out var condition) ||
                condition.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            var controllingKey = condition
                .GetProperty("field")
                .GetString()!;

            return values.TryGetProperty(controllingKey, out var actual) &&
                condition.TryGetProperty("equals", out var expected) &&
                ScalarEquals(actual, expected);
        }

        private static bool ScalarEquals(
            JsonElement actual,
            JsonElement expected)
        {
            if (actual.ValueKind != expected.ValueKind)
            {
                return false;
            }

            return actual.ValueKind switch
            {
                JsonValueKind.String =>
                    actual.GetString() == expected.GetString(),
                JsonValueKind.Number =>
                    actual.TryGetDecimal(out var actualNumber) &&
                    expected.TryGetDecimal(out var expectedNumber) &&
                    actualNumber == expectedNumber,
                JsonValueKind.True or JsonValueKind.False =>
                    actual.GetBoolean() == expected.GetBoolean(),
                JsonValueKind.Null => true,
                _ => false
            };
        }

        private static long ReadMaximumFileSize(JsonElement field)
        {
            if (!field.TryGetProperty("maxSizeMb", out var configured) ||
                !configured.TryGetInt32(out var maximumMb) ||
                maximumMb <= 0)
            {
                return SubmissionFileStorageService.MaxFileSize;
            }

            var configuredBytes = (long)maximumMb * 1024 * 1024;

            return Math.Min(
                configuredBytes,
                SubmissionFileStorageService.MaxFileSize);
        }

        private static bool AcceptsContentType(
            JsonElement field,
            string contentType)
        {
            if (!field.TryGetProperty("accept", out var accept) ||
                accept.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            IEnumerable<string> configured = accept.ValueKind switch
            {
                JsonValueKind.String => accept.GetString()!
                    .Split(',', StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries),
                JsonValueKind.Array => accept.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString()!),
                _ => []
            };

            return configured.Any(item =>
                AcceptEntryMatches(item, contentType));
        }

        private static bool AcceptEntryMatches(
            string configured,
            string contentType)
        {
            if (string.Equals(
                configured,
                contentType,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return configured.ToLowerInvariant() switch
            {
                ".pdf" => contentType == "application/pdf",
                ".png" => contentType == "image/png",
                ".jpg" or ".jpeg" => contentType == "image/jpeg",
                ".docx" => contentType ==
                    "application/vnd.openxmlformats-officedocument." +
                    "wordprocessingml.document",
                ".odt" => contentType ==
                    "application/vnd.oasis.opendocument.text",
                "image/*" => contentType.StartsWith(
                    "image/",
                    StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

        private static string? NormalizeFileName(string fileName)
        {
            var normalized = fileName
                .Replace('\\', '/')
                .Split('/')
                .LastOrDefault();

            return string.IsNullOrWhiteSpace(normalized) ||
                normalized.Length > 255
                    ? null
                    : normalized;
        }

        private async Task DeleteStoredAsync(
            IEnumerable<StoredUpload> uploads)
        {
            foreach (var item in uploads)
            {
                await storage.DeleteAsync(item.Stored.StorageKey);
            }
        }

        private async Task DeleteStoredFilesAsync(
            IEnumerable<StoredSubmissionFile> files)
        {
            foreach (var file in files)
            {
                await storage.DeleteAsync(file.StorageKey);
            }
        }

        private static void MergeErrors(
            IDictionary<string, List<string>> target,
            IReadOnlyDictionary<string, string[]> source)
        {
            foreach (var item in source)
            {
                foreach (var message in item.Value)
                {
                    AddError(target, item.Key, message);
                }
            }
        }

        private static void AddError(
            IDictionary<string, List<string>> errors,
            string key,
            string message)
        {
            if (!errors.TryGetValue(key, out var messages))
            {
                messages = [];
                errors[key] = messages;
            }

            messages.Add(message);
        }

        private static IReadOnlyDictionary<string, string[]> ToReadOnly(
            IReadOnlyDictionary<string, List<string>> errors)
        {
            return errors.ToDictionary(
                item => item.Key,
                item => item.Value.ToArray());
        }

        private ActionResult FileTooLarge()
        {
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status413PayloadTooLarge,
                    "Fișier prea mare",
                    "Dimensiunea maximă permisă este de 10 MB per fișier."));
        }

        private ActionResult? RejectAdditionalProperties(
            IReadOnlyDictionary<string, JsonElement>? properties)
        {
            if (properties is null || properties.Count == 0)
            {
                return null;
            }

            return BadRequest(ApiProblemDetails.CreateValidation(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "Câmpuri nepermise",
                "Corpul cererii conține câmpuri care sunt generate de server.",
                properties.ToDictionary(
                    item => item.Key,
                    _ => new[] { "Câmpul nu este acceptat în această cerere." })));
        }

        private ActionResult ValuesValidationFailure(
            IReadOnlyDictionary<string, string[]> errors)
        {
            return UnprocessableEntity(
                ApiProblemDetails.CreateValidation(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Valori de formular invalide",
                    "Formularul nu respectă schema serviciului.",
                    errors.ToDictionary(
                        item => item.Key,
                        item => item.Value)));
        }

        private ActionResult EmailConfirmationRequired()
        {
            return UnprocessableEntity(ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status422UnprocessableEntity,
                "Confirmare e-mail necesară",
                "Confirmă adresa de e-mail înainte să depui o cerere."));
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
                [
                    "pageSize trebuie să fie între 1 și 100."
                ];
            }

            return UnprocessableEntity(
                ApiProblemDetails.CreateValidation(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Paginare invalidă",
                    "Parametrii de paginare sunt invalizi.",
                    errors));
        }

        private ProblemDetails ServiceNotFound()
        {
            return ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status404NotFound,
                "Serviciu indisponibil",
                "Serviciul nu există sau nu este publicat.");
        }

        private ProblemDetails SubmissionNotFound()
        {
            return ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status404NotFound,
                "Cerere inexistentă",
                "Cererea solicitată nu există.");
        }

        private ProblemDetails FileNotFoundProblem()
        {
            return ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status404NotFound,
                "Fișier inexistent",
                "Fișierul solicitat nu există pentru această cerere.");
        }

        private sealed record PendingSubmissionFile(
            Guid Id,
            IFormFile Upload,
            string? FieldKey,
            string OriginalName,
            long MaximumSize,
            JsonElement? FieldSchema);

        private sealed record StoredUpload(
            PendingSubmissionFile Pending,
            StoredSubmissionFile Stored);
    }
}
