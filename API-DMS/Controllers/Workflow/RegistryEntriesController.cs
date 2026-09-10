using API_DMS.Data;
using API_DMS.DTO.RegistryEntries;
using API_DMS.Entities;
using API_DMS.Services;
using API_DMS.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using API_DMS.Filters;

namespace API_DMS.Controllers.Workflow
{
    [ApiController]
    [Route("api/registry-entries")]
    [Authorize(Roles = "Clerk,Admin")]
    public class RegistryEntriesController : ControllerBase
    {
        private readonly DmsDbContext db;
        private readonly RegistryNumberAllocator numberAllocator;
        private readonly FileStorageService storage;
        private readonly RegistryEntryWorkflowService workflow;
        private readonly RegistryTaskService taskService;
        private readonly FileDownloadTokenService downloadTokens;

        public RegistryEntriesController(
            DmsDbContext db,
            RegistryNumberAllocator numberAllocator,
            FileStorageService storage,
            RegistryEntryWorkflowService workflow,
            RegistryTaskService taskService,
            FileDownloadTokenService downloadTokens)
        {
            this.db = db;
            this.numberAllocator = numberAllocator;
            this.storage = storage;
            this.workflow = workflow;
            this.taskService = taskService;
            this.downloadTokens = downloadTokens;
        }

        [HttpPost]
        public async Task<ActionResult<RegistryEntryResponse>> Create(
            CreateRegistryEntryRequest request)
        {
            var userIdValue = User.FindFirstValue(
                JwtRegisteredClaimNames.Sub);

            if (!Guid.TryParse(userIdValue, out var userId))
            {
                return Unauthorized();
            }

            if (!Enum.IsDefined(request.Direction))
            {
                return UnprocessableEntity(new
                {
                    error = "Direcția poziției este invalidă."
                });
            }

            var registry = await db.RegistryTypes
                .AsNoTracking()
                .SingleOrDefaultAsync(x =>
                    x.id == request.RegistryTypeId);

            if (registry is null)
            {
                return UnprocessableEntity(new
                {
                    error = "Registrul nu există."
                });
            }

            if (registry.is_closed)
            {
                return UnprocessableEntity(new
                {
                    error = "Registrul este închis."
                });
            }

            var directionAllowed =
                registry.direction == RegistryDirection.Both ||
                registry.direction == RegistryDirection.In &&
                request.Direction == EntryDirection.In ||
                registry.direction == RegistryDirection.Out &&
                request.Direction == EntryDirection.Out;

            if (!directionAllowed)
            {
                return UnprocessableEntity(new
                {
                    error = "Direcția poziției nu este permisă de registru."
                });
            }

            if (request.SourceDocDate >
                DateOnly.FromDateTime(DateTime.UtcNow))
            {
                return UnprocessableEntity(new
                {
                    error = "Data documentului nu poate fi în viitor."
                });
            }

            if (request.DepartmentId.HasValue)
            {
                var departmentExists = await db.Departments
                    .AnyAsync(x =>
                        x.id == request.DepartmentId.Value &&
                        x.is_active);

                if (!departmentExists)
                {
                    return UnprocessableEntity(new
                    {
                        error = "Compartimentul nu există sau este inactiv."
                    });
                }
            }

            var registeredAt = DateTimeOffset.UtcNow;
            var registrationDate =
                DateOnly.FromDateTime(registeredAt.UtcDateTime);

            if (request.Deadline is { } deadline &&
                deadline < registrationDate)
            {
                return UnprocessableEntity(new
                {
                    error = "Termenul nu poate fi anterior datei înregistrării."
                });
            }

            var year = registeredAt.UtcDateTime.Year;
            var defaultDeadline = registrationDate.AddDays(
                registry.default_deadline_days);

            await using var transaction =
                await db.Database.BeginTransactionAsync();

            var number = await numberAllocator.AllocateAsync(
                registry.id,
                year);

            var entry = new RegistryEntry
            {
                id = Guid.NewGuid(),
                registry_type_id = registry.id,
                year = year,
                number = number,
                direction = request.Direction,
                registered_at = registeredAt,
                submitted_at = request.SubmittedAt,
                subject = request.Subject.Trim(),
                applicant_name = request.ApplicantName.Trim(),
                applicant_national_id =
                    request.ApplicantNationalId?.Trim(),
                applicant_email =
                    request.ApplicantEmail?.Trim(),
                applicant_phone =
                    request.ApplicantPhone?.Trim(),
                applicant_address =
                    request.ApplicantAddress?.Trim(),
                source_doc_number =
                    request.SourceDocNumber?.Trim(),
                source_doc_date = request.SourceDocDate,
                department_id = request.DepartmentId,
                service_code = request.ServiceCode?.Trim(),
                form_values = request.FormValues.HasValue
                    ? JsonDocument.Parse(
                        request.FormValues.Value.GetRawText())
                    : null,
                deadline = request.Deadline ?? defaultDeadline,
                status = EntryStatus.Registered,
                created_by_user_id = userId
            };

            db.RegistryEntries.Add(entry);

            db.EntryEvents.Add(new EntryEvent
            {
                id = Guid.NewGuid(),
                entry_id = entry.id,
                occurred_at = registeredAt,
                actor_user_id = userId,
                type = EventType.Created,
                message = "Poziția a fost creată.",
                payload = JsonDocument.Parse("{}")
            });

            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            var response = new RegistryEntryResponse(
                entry.id,
                entry.registry_type_id,
                entry.year,
                entry.number,
                $"{entry.number}/{entry.year}",
                entry.direction,
                entry.registered_at,
                entry.deadline,
                entry.subject,
                entry.applicant_name,
                entry.status);

            return CreatedAtAction(
                nameof(GetById),
                new { id = entry.id },
                response);
        }

        [HttpGet]
        public async Task<ActionResult<PagedResponse<RegistryEntryListItem>>> GetAll(
            [FromQuery] RegistryEntryListQuery request)
        {
            if (request.Page < 1)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Parametri invalizi",
                    Detail = "Page trebuie să fie mai mare sau egal cu 1."
                });
            }

            if (
                request.PageSize < 1 ||
                request.PageSize > 100)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Parametri invalizi",
                    Detail = "PageSize trebuie să fie între 1 și 100."
                });
            }

            if (request.FromDate.HasValue &&
                request.ToDate.HasValue &&
                request.FromDate > request.ToDate)
            {
                return UnprocessableEntity(new
                {
                    error = "Intervalul de date este invalid."
                });
            }

            var query = db.RegistryEntries
                .AsNoTracking()
                .AsQueryable();

            if (request.RegistryTypeId.HasValue)
            {
                query = query.Where(x =>
                    x.registry_type_id == request.RegistryTypeId.Value);
            }

            if (request.Year.HasValue)
            {
                query = query.Where(x =>
                    x.year == request.Year.Value);
            }

            if (request.FromDate.HasValue)
            {
                var from = new DateTimeOffset(
                    request.FromDate.Value.ToDateTime(TimeOnly.MinValue),
                    TimeSpan.Zero);

                query = query.Where(x =>
                    x.registered_at >= from);
            }

            if (request.ToDate.HasValue)
            {
                var toExclusive = new DateTimeOffset(
                    request.ToDate.Value.AddDays(1)
                        .ToDateTime(TimeOnly.MinValue),
                    TimeSpan.Zero);

                query = query.Where(x =>
                    x.registered_at < toExclusive);
            }

            if (request.MinNumber.HasValue)
            {
                query = query.Where(x =>
                    x.number >= request.MinNumber.Value);
            }

            if (request.MaxNumber.HasValue)
            {
                query = query.Where(x =>
                    x.number <= request.MaxNumber.Value);
            }

            if (request.Status.HasValue)
            {
                query = query.Where(x =>
                    x.status == request.Status.Value);
            }

            if (request.MinNumber.HasValue &&
                request.MaxNumber.HasValue &&
                request.MinNumber.Value > request.MaxNumber.Value)
            {
                return UnprocessableEntity(new
                {
                    error = "Intervalul de numere este invalid."
                });
            }

            if (request.DepartmentId.HasValue)
            {
                query = query.Where(x =>
                    x.department_id == request.DepartmentId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var pattern = $"%{request.Search.Trim()}%";

                query = query.Where(x =>
                    EF.Functions.ILike(x.subject, pattern) ||
                    EF.Functions.ILike(x.applicant_name, pattern));
            }

            query = (request.Sort?.ToLowerInvariant() switch
            {
                "number:asc" =>
                    query.OrderBy(x => x.number),

                "number:desc" =>
                    query.OrderByDescending(x => x.number),

                "subject:asc" =>
                    query.OrderBy(x => x.subject),

                "subject:desc" =>
                    query.OrderByDescending(x => x.subject),

                "registeredat:asc" =>
                    query.OrderBy(x => x.registered_at),

                _ =>
                    query.OrderByDescending(x => x.registered_at)
            })
            .ThenBy(x => x.id);

            var total = await query.CountAsync();

            var rows = await query
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(x => new
                {
                    x.id,
                    x.registry_type_id,
                    RegistryTypeCode = x.registry_type.code,
                    RegistryTypeName = x.registry_type.name,
                    x.year,
                    x.number,
                    x.direction,
                    x.registered_at,
                    x.deadline,
                    x.subject,
                    x.applicant_name,
                    x.status
                })
                .ToListAsync();

            var items = rows
                .Select(x => new RegistryEntryListItem(
                    x.id,
                    x.registry_type_id,
                    x.RegistryTypeCode,
                    x.RegistryTypeName,
                    x.year,
                    x.number,
                    $"{x.number}/{x.year}",
                    x.direction,
                    x.registered_at,
                    x.deadline,
                    x.subject,
                    x.applicant_name,
                    x.status))
                .ToList();

            return Ok(new PagedResponse<RegistryEntryListItem>(
                items,
                request.Page,
                request.PageSize,
                total));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<RegistryEntryDetailsResponse>> GetById(
            Guid id)
        {
            var entry = await db.RegistryEntries
                .AsNoTracking()
                .Where(x => x.id == id)
                .Select(x => new
                {
                    x.id,
                    x.external_id,

                    x.registry_type_id,
                    RegistryTypeCode = x.registry_type.code,
                    RegistryTypeName = x.registry_type.name,

                    x.year,
                    x.number,
                    x.direction,

                    x.registered_at,
                    x.submitted_at,

                    x.subject,
                    x.applicant_name,
                    x.applicant_national_id,
                    x.applicant_email,
                    x.applicant_phone,
                    x.applicant_address,

                    x.source_doc_number,
                    x.source_doc_date,

                    x.department_id,
                    DepartmentCode = x.department == null
                        ? null
                        : x.department.code,
                    DepartmentName = x.department == null
                        ? null
                        : x.department.name,

                    x.service_code,
                    x.form_values,

                    x.deadline,
                    x.status,
                    x.status_note,
                    x.created_by_user_id
                })
                .SingleOrDefaultAsync();

            if (entry is null)
            {
                return NotFound();
            }

            var documents = await db.RegistryDocuments
                .AsNoTracking()
                .Where(x => x.entry_id == id)
                .OrderBy(x => x.direction)
                .ThenBy(x => x.document_date)
                .Select(x => new RegistryDocumentDetailsResponse(
                    x.id,
                    x.external_file_id,
                    x.direction,
                    x.document_kind_id,
                    x.document_kind.code,
                    x.document_kind.name,
                    x.document_date,
                    x.issuer,
                    x.note,
                    x.original_name,
                    x.content_type,
                    x.size_bytes,
                    x.sha256,
                    x.uploaded_by_user_id))
                .ToListAsync();

            var tasks = await db.Tasks
                .AsNoTracking()
                .Where(x => x.entry_id == id)
                .OrderBy(x => x.status)
                .ThenBy(x => x.due_date)
                .Select(x => new RegistryTaskDetailsResponse(
                    x.id,
                    x.assignee_user_id,
                    x.assignee_user == null
                        ? null
                        : x.assignee_user.email,
                    x.department_id,
                    x.department == null
                        ? null
                        : x.department.code,
                    x.department == null
                        ? null
                        : x.department.name,
                    x.title,
                    x.instructions,
                    x.due_date,
                    x.status,
                    x.resolution_note,
                    x.completed_at,
                    x.created_by_user_id))
                .ToListAsync();

            var events = await db.EntryEvents
                .AsNoTracking()
                .Where(x => x.entry_id == id)
                .OrderBy(x => x.occurred_at)
                .Select(x => new EntryEventDetailsResponse(
                    x.id,
                    x.occurred_at,
                    x.actor_user_id,
                    x.type,
                    x.message,
                    x.payload))
                .ToListAsync();

            var response = new RegistryEntryDetailsResponse(
                entry.id,
                entry.external_id,

                entry.registry_type_id,
                entry.RegistryTypeCode,
                entry.RegistryTypeName,

                entry.year,
                entry.number,
                $"{entry.number}/{entry.year}",
                entry.direction,

                entry.registered_at,
                entry.submitted_at,

                entry.subject,
                entry.applicant_name,
                entry.applicant_national_id,
                entry.applicant_email,
                entry.applicant_phone,
                entry.applicant_address,

                entry.source_doc_number,
                entry.source_doc_date,

                entry.department_id,
                entry.DepartmentCode,
                entry.DepartmentName,

                entry.service_code,
                entry.form_values,

                entry.deadline,
                entry.status,
                entry.status_note,

                entry.created_by_user_id,

                documents,
                tasks,
                events);

            return Ok(response);
        }

        [HttpPost("{id:guid}/take")]
        public Task<ActionResult<RegistryEntryTransitionResponse>> Take(
            Guid id,
            CancellationToken cancellationToken)
        {
            return ChangeStatusAsync(
                id,
                EntryStatus.InReview,
                null,
                cancellationToken);
        }

        [HttpPost("{id:guid}/request-info")]
        public Task<ActionResult<RegistryEntryTransitionResponse>> RequestInfo(
            Guid id,
            StatusNoteRequest? request,
            CancellationToken cancellationToken)
        {
            return ChangeStatusAsync(
                id,
                EntryStatus.InfoRequested,
                request?.Note,
                cancellationToken);
        }

        [HttpPost("{id:guid}/reject")]
        public Task<ActionResult<RegistryEntryTransitionResponse>> Reject(
            Guid id,
            StatusNoteRequest? request,
            CancellationToken cancellationToken)
        {
            return ChangeStatusAsync(
                id,
                EntryStatus.Rejected,
                request?.Note,
                cancellationToken);
        }

        [HttpPost("{id:guid}/complete")]
        public Task<ActionResult<RegistryEntryTransitionResponse>> Complete(
            Guid id,
            CancellationToken cancellationToken)
        {
            return ChangeStatusAsync(
                id,
                EntryStatus.Completed,
                null,
                cancellationToken);
        }

        [HttpPost("{id:guid}/cancel")]
        public Task<ActionResult<RegistryEntryTransitionResponse>> Cancel(
            Guid id,
            StatusNoteRequest? request,
            CancellationToken cancellationToken)
        {
            return ChangeStatusAsync(
                id,
                EntryStatus.Cancelled,
                request?.Note,
                cancellationToken);
        }

        private async Task<ActionResult<RegistryEntryTransitionResponse>>
            ChangeStatusAsync(
                Guid entryId,
                EntryStatus targetStatus,
                string? note,
                CancellationToken cancellationToken)
        {
            var userIdValue = User.FindFirstValue(
                JwtRegisteredClaimNames.Sub);

            if (!Guid.TryParse(userIdValue, out var userId))
            {
                return Unauthorized();
            }

            try
            {
                var result = await workflow.TransitionAsync(
                    entryId,
                    targetStatus,
                    note,
                    userId,
                    cancellationToken);

                if (result is null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Status = StatusCodes.Status404NotFound,
                        Title = "Poziție inexistentă",
                        Detail = "Poziția de registratură nu există."
                    });
                }

                return Ok(new RegistryEntryTransitionResponse(
                    result.EntryId,
                    result.PreviousStatus,
                    result.CurrentStatus,
                    result.StatusNote,
                    result.OccurredAt));
            }
            catch (RegistryEntryWorkflowRuleException exception)
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Tranziție invalidă",
                    Detail = exception.Message
                };

                problem.Extensions["traceId"] = HttpContext.TraceIdentifier;

                return UnprocessableEntity(problem);
            }
        }

        private static RegistryDocumentResponse ToResponse(
            RegistryDocument document,
            DocumentKind documentKind)
        {
            return new RegistryDocumentResponse(
                document.id,
                document.entry_id,
                document.external_file_id,
                document.direction,
                document.document_kind_id,
                documentKind.code,
                documentKind.name,
                document.document_date,
                document.issuer,
                document.note,
                document.original_name,
                document.content_type,
                document.size_bytes,
                document.sha256,
                document.uploaded_by_user_id);
        }

        [HttpPost("{entryId:guid}/documents")]
        [Consumes("multipart/form-data")]
        [ServiceFilter(typeof(UploadSizeFilter))]
        [RequestSizeLimit(FileStorageService.MaxUploadRequestSize)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
        [RejectUnknownUploadFields]
        public async Task<ActionResult<RegistryDocumentResponse>> UploadDocument(
            Guid entryId,
            [FromForm] UploadRegistryDocumentRequest request,
            CancellationToken cancellationToken)
        {
            var userIdValue = User.FindFirstValue(
                JwtRegisteredClaimNames.Sub);

            if (!Guid.TryParse(userIdValue, out var userId))
            {
                return Unauthorized();
            }

            if (request.File is null)
            {
                return UnprocessableEntity(new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Fișier invalid",
                    Detail = "Fișierul este obligatoriu."
                });
            }

            if (request.File.Length == 0)
            {
                return UnprocessableEntity(new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Fișier invalid",
                    Detail = "Fișierul este gol."
                });
            }

            if (request.File.Length >
                FileStorageService.MaxFileSize)
            {
                return StatusCode(
                    StatusCodes.Status413PayloadTooLarge,
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status413PayloadTooLarge,
                        Title = "Fișier prea mare",
                        Detail = "Dimensiunea maximă este de 10 MB."
                    });
            }

            if (!Enum.IsDefined(request.Direction))
            {
                return UnprocessableEntity(new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Direcție invalidă",
                    Detail = "Direcția documentului este invalidă."
                });
            }

            var entryExists = await db.RegistryEntries
                .AnyAsync(
                    x => x.id == entryId,
                    cancellationToken);

            if (!entryExists)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Poziție inexistentă",
                    Detail = "Poziția de registratură nu există."
                });
            }

            var documentKind = await db.DocumentKinds
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.id == request.DocumentKindId &&
                         x.is_active,
                    cancellationToken);

            if (documentKind is null)
            {
                return UnprocessableEntity(new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Tip de document invalid",
                    Detail = "Tipul de document nu există sau este inactiv."
                });
            }

            if (request.ExternalFileId.HasValue)
            {
                var externalFileExists =
                    await db.RegistryDocuments.AnyAsync(
                        x => x.external_file_id ==
                             request.ExternalFileId.Value,
                        cancellationToken);

                if (externalFileExists)
                {
                    return Conflict(new ProblemDetails
                    {
                        Status = StatusCodes.Status409Conflict,
                        Title = "Fișier duplicat",
                        Detail = "ExternalFileId este deja utilizat."
                    });
                }
            }

            if (request.DocumentDate >
                DateOnly.FromDateTime(DateTime.UtcNow))
            {
                return UnprocessableEntity(new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Dată invalidă",
                    Detail = "Data documentului nu poate fi în viitor."
                });
            }

            var originalName = request.File.FileName
                .Replace('\\', '/')
                .Split('/')
                .Last();

            if (string.IsNullOrWhiteSpace(originalName) ||
                originalName.Length > 255)
            {
                return UnprocessableEntity(new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Nume de fișier invalid",
                    Detail = "Numele fișierului este invalid."
                });
            }

            StoredFile storedFile;

            try
            {
                storedFile = await storage.StoreAsync(
                    request.File,
                    cancellationToken);
            }
            catch (InvalidStoredFileException exception)
            {
                return UnprocessableEntity(new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Fișier invalid",
                    Detail = exception.Message
                });
            }

            var documentId = Guid.NewGuid();

            try
            {
                await using var transaction =
                    await db.Database.BeginTransactionAsync(
                        cancellationToken);

                var document = new RegistryDocument
                {
                    id = documentId,
                    entry_id = entryId,
                    external_file_id = request.ExternalFileId,
                    direction = request.Direction,
                    document_kind_id = request.DocumentKindId,
                    document_date = request.DocumentDate,
                    issuer = request.Issuer?.Trim(),
                    note = request.Note?.Trim(),
                    storage_key = storedFile.StorageKey,
                    original_name = originalName,
                    content_type = storedFile.ContentType,
                    size_bytes = storedFile.SizeBytes,
                    sha256 = storedFile.Sha256,
                    uploaded_by_user_id = userId
                };

                db.RegistryDocuments.Add(document);

                db.EntryEvents.Add(new EntryEvent
                {
                    id = Guid.NewGuid(),
                    entry_id = entryId,
                    occurred_at = DateTimeOffset.UtcNow,
                    actor_user_id = userId,
                    type = EventType.DocumentAdded,
                    message = "A fost adăugat un document.",
                    payload = JsonSerializer.SerializeToDocument(
                        new
                        {
                            documentId,
                            documentKindId = request.DocumentKindId,
                            direction = request.Direction.ToString()
                        })
                });

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Created(
                    $"/api/registry-entries/{entryId}/documents/{documentId}",
                    ToResponse(document, documentKind));
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException postgres &&
                      postgres.SqlState == "23505")
            {
                await storage.DeleteAsync(storedFile.StorageKey);

                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Document duplicat",
                    Detail = "Documentul există deja."
                });
            }
            catch
            {
                await storage.DeleteAsync(storedFile.StorageKey);
                throw;
            }
        }

        [HttpGet("{entryId:guid}/documents/{documentId:guid}")]
        public async Task<ActionResult<RegistryDocumentResponse>> GetDocument(
            Guid entryId,
            Guid documentId,
            CancellationToken cancellationToken)
        {
            var document = await db.RegistryDocuments
                .AsNoTracking()
                .Where(x =>
                    x.id == documentId &&
                    x.entry_id == entryId)
                .Select(x => new RegistryDocumentResponse(
                    x.id,
                    x.entry_id,
                    x.external_file_id,
                    x.direction,
                    x.document_kind_id,
                    x.document_kind.code,
                    x.document_kind.name,
                    x.document_date,
                    x.issuer,
                    x.note,
                    x.original_name,
                    x.content_type,
                    x.size_bytes,
                    x.sha256,
                    x.uploaded_by_user_id))
                .SingleOrDefaultAsync(cancellationToken);

            if (document is null)
            {
                return NotFound();
            }

            return Ok(document);
        }

        [HttpPost("{entryId:guid}/tasks")]
        public async Task<ActionResult<RegistryTaskResponse>> CreateTask(
    Guid entryId,
    CreateRegistryTaskRequest? request,
    CancellationToken cancellationToken)
        {
            var actorUserId = GetCurrentUserId();

            if (actorUserId is null)
            {
                return Unauthorized();
            }

            if (request is null)
            {
                return UnprocessableEntity(CreateProblem(
                    "Task invalid",
                    "Datele task-ului sunt obligatorii."));
            }

            try
            {
                var result = await taskService.CreateAsync(
                    entryId,
                    request,
                    actorUserId.Value,
                    cancellationToken);

                if (result is null)
                {
                    return NotFound(CreateProblem(
                        "Poziție inexistentă",
                        "Poziția de registratură nu există."));
                }

                return Created(
                    $"/api/registry-entries/{entryId}/tasks/{result.Id}",
                    result);
            }
            catch (RegistryTaskRuleException exception)
            {
                return UnprocessableEntity(CreateProblem(
                    "Repartizare invalidă",
                    exception.Message));
            }
        }

        [HttpPost("{entryId:guid}/tasks/{taskId:guid}/complete")]
        public async Task<ActionResult<RegistryTaskResponse>> CompleteTask(
            Guid entryId,
            Guid taskId,
            CompleteRegistryTaskRequest? request,
            CancellationToken cancellationToken)
        {
            var actorUserId = GetCurrentUserId();

            if (actorUserId is null)
            {
                return Unauthorized();
            }

            if (request is null)
            {
                return UnprocessableEntity(CreateProblem(
                    "Rezolvare invalidă",
                    "Nota de rezolvare este obligatorie."));
            }

            try
            {
                var result = await taskService.CompleteAsync(
                    entryId,
                    taskId,
                    request,
                    actorUserId.Value,
                    cancellationToken);

                if (result is null)
                {
                    return NotFound(CreateProblem(
                        "Task inexistent",
                        "Task-ul nu aparține poziției indicate."));
                }

                return Ok(result);
            }
            catch (RegistryTaskRuleException exception)
            {
                return UnprocessableEntity(CreateProblem(
                    "Rezolvare invalidă",
                    exception.Message));
            }
        }

        private Guid? GetCurrentUserId()
        {
            var value = User.FindFirstValue(
                JwtRegisteredClaimNames.Sub);

            return Guid.TryParse(value, out var userId)
                ? userId
                : null;
        }

        private ProblemDetails CreateProblem(
            string title,
            string detail)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = title,
                Detail = detail
            };

            problem.Extensions["traceId"] =
                HttpContext.TraceIdentifier;

            return problem;
        }

        [HttpPost("{entryId:guid}/documents/{documentId:guid}/download-url")]
        public async Task<ActionResult<SignedDownloadUrlResponse>>
            CreateDownloadUrl(
                Guid entryId,
                Guid documentId,
                CancellationToken cancellationToken)
        {
            var document = await db.RegistryDocuments
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.id == documentId &&
                         x.entry_id == entryId,
                    cancellationToken);

            if (document is null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Document inexistent",
                    Detail = "Documentul nu există pentru această poziție."
                });
            }

            var result = downloadTokens.Create(documentId);

            var url =
                $"{Request.Scheme}://{Request.Host}" +
                $"/api/documents/{documentId}/content" +
                $"?token={Uri.EscapeDataString(result.Token)}";

            return Ok(new SignedDownloadUrlResponse(
                documentId,
                url,
                result.ExpiresAt));
        }

        [AllowAnonymous]
        [HttpGet("/api/documents/{documentId:guid}/content")]
        public async Task<IActionResult> DownloadDocument(
            Guid documentId,
            [FromQuery] string? token,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(token) ||
                !downloadTokens.TryValidate(
                    token,
                    documentId,
                    out _))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = "URL invalid sau expirat",
                        Detail = "URL-ul de descărcare nu mai este valid."
                    });
            }

            var document = await db.RegistryDocuments
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.id == documentId,
                    cancellationToken);

            if (document is null)
            {
                return NotFound();
            }

            try
            {
                var stream = storage.OpenRead(
                    document.storage_key);

                return File(
                    stream,
                    document.content_type,
                    document.original_name,
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
    }
}
