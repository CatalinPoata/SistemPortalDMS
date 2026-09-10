using API_PORTAL.Data;
using API_PORTAL.DTO.ServiceDefinitions;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using API_PORTAL.Errors;
using API_PORTAL.Integration;
using API_PORTAL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace API_PORTAL.Controllers.ServiceDefinitions
{
    [ApiController]
    [Route("api/service-definitions")]
    [Authorize(Roles = nameof(Role.Admin))]
    public sealed class ServiceDefinitionsController : ControllerBase
    {
        private static readonly Regex CodePattern = new(
            "^[a-z0-9]+(?:-[a-z0-9]+)*$",
            RegexOptions.CultureInvariant);

        private readonly PortalDbContext db;
        private readonly ServiceFormSchemaValidator schemaValidator;
        private readonly IServiceHtmlSanitizer htmlSanitizer;
        private readonly IDmsRegistryTypeClient dmsRegistryTypes;

        public ServiceDefinitionsController(
            PortalDbContext db,
            ServiceFormSchemaValidator schemaValidator,
            IServiceHtmlSanitizer htmlSanitizer,
            IDmsRegistryTypeClient dmsRegistryTypes)
        {
            this.db = db;
            this.schemaValidator = schemaValidator;
            this.htmlSanitizer = htmlSanitizer;
            this.dmsRegistryTypes = dmsRegistryTypes;
        }

        [HttpGet]
        public async Task<ActionResult<
            PagedResponse<ServiceDefinitionListItemResponse>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? sort = null,
            CancellationToken cancellationToken = default)
        {
            var errors = ValidatePage(page, pageSize, sort);

            if (errors.Count > 0)
            {
                return ValidationFailure(errors);
            }

            var query = db.ServiceDefinitions.AsNoTracking();
            var total = await query.CountAsync(cancellationToken);
            var ordered = ApplySort(query, sort);

            var items = await ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(service => new ServiceDefinitionListItemResponse(
                    service.id,
                    service.code,
                    service.title,
                    service.short_description,
                    service.registry_type_code,
                    service.schema_version,
                    service.requires_attachment,
                    service.max_attachments,
                    service.is_published,
                    service.display_order,
                    service.created_at,
                    service.updated_at))
                .ToListAsync(cancellationToken);

            return Ok(new PagedResponse<ServiceDefinitionListItemResponse>(
                items,
                page,
                pageSize,
                total));
        }

        [HttpGet("dms-registry-types")]
        public async Task<ActionResult<IReadOnlyList<
            DmsRegistryTypeListItem>>> GetDmsRegistryTypes(
            CancellationToken cancellationToken)
        {
            var result = await dmsRegistryTypes.GetAllAsync(
                cancellationToken);

            if (!result.IsAvailable)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    ApiProblemDetails.Create(
                        HttpContext,
                        StatusCodes.Status503ServiceUnavailable,
                        "DMS indisponibil",
                        "Registrele DMS nu pot fi încărcate în acest moment."));
            }

            return Ok(result.Items);
        }

        [HttpGet("{code}")]
        public async Task<ActionResult<ServiceDefinitionDetailsResponse>>
            GetByCode(
                string code,
                CancellationToken cancellationToken)
        {
            var service = await db.ServiceDefinitions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.code == code,
                    cancellationToken);

            if (service is null)
            {
                return NotFound(ServiceNotFound());
            }

            return Ok(await ToDetailsAsync(service, cancellationToken));
        }

        [HttpPost]
        public async Task<ActionResult<ServiceDefinitionDetailsResponse>>
            Create(
                CreateServiceDefinitionRequest request,
                CancellationToken cancellationToken)
        {
            var code = request.Code?.Trim().ToLowerInvariant() ?? "";
            var title = request.Title?.Trim() ?? "";
            var registryTypeCode = request.RegistryTypeCode?
                .Trim()
                .ToUpperInvariant() ?? "";
            var errors = ValidateWriteRequest(
                code,
                title,
                registryTypeCode,
                request.FormSchema,
                request.RequiresAttachment,
                request.MaxAttachments);

            if (errors.Count > 0)
            {
                return ValidationFailure(errors);
            }

            if (await db.ServiceDefinitions.AnyAsync(
                    item => item.code == code,
                    cancellationToken))
            {
                return Conflict(DuplicateCode());
            }

            var service = new ServiceDefinition
            {
                id = Guid.NewGuid(),
                code = code,
                title = title,
                short_description = Normalize(request.ShortDescription),
                description = htmlSanitizer.Sanitize(request.Description),
                registry_type_code = registryTypeCode,
                form_schema = JsonDocument.Parse(
                    request.FormSchema!.Value.GetRawText()),
                schema_version = 1,
                requires_attachment = request.RequiresAttachment,
                max_attachments = request.MaxAttachments,
                is_published = false,
                display_order = request.DisplayOrder
            };

            db.ServiceDefinitions.Add(service);

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
                new { code = service.code },
                await ToDetailsAsync(service, cancellationToken));
        }

        [HttpPut("{code}")]
        public async Task<ActionResult<ServiceDefinitionDetailsResponse>>
            Update(
                string code,
                UpdateServiceDefinitionRequest request,
                CancellationToken cancellationToken)
        {
            var service = await db.ServiceDefinitions
                .SingleOrDefaultAsync(
                    item => item.code == code,
                    cancellationToken);

            if (service is null)
            {
                return NotFound(ServiceNotFound());
            }

            if (service.schema_version != request.ExpectedSchemaVersion)
            {
                return Conflict(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status409Conflict,
                    "Schema a fost modificată între timp",
                    "Reîncarcă serviciul înainte să salvezi modificările."));
            }

            var title = request.Title?.Trim() ?? "";
            var registryTypeCode = request.RegistryTypeCode?
                .Trim()
                .ToUpperInvariant() ?? "";
            var errors = ValidateWriteRequest(
                service.code,
                title,
                registryTypeCode,
                request.FormSchema,
                request.RequiresAttachment,
                request.MaxAttachments);

            if (errors.Count > 0)
            {
                return ValidationFailure(errors);
            }

            var schemaChanged = service.form_schema.RootElement
                .GetRawText() != request.FormSchema!.Value.GetRawText();

            if (service.is_published && schemaChanged)
            {
                var schemaErrors = schemaValidator.ValidateForPublication(
                    request.FormSchema.Value);

                if (schemaErrors.Count > 0)
                {
                    return SchemaValidationFailure(schemaErrors);
                }
            }

            service.title = title;
            service.short_description = Normalize(request.ShortDescription);
            service.description = htmlSanitizer.Sanitize(request.Description);
            service.registry_type_code = registryTypeCode;
            service.form_schema = JsonDocument.Parse(
                request.FormSchema.Value.GetRawText());
            service.requires_attachment = request.RequiresAttachment;
            service.max_attachments = request.MaxAttachments;
            service.display_order = request.DisplayOrder;

            if (schemaChanged)
            {
                service.schema_version++;
            }

            await db.SaveChangesAsync(cancellationToken);

            return Ok(await ToDetailsAsync(service, cancellationToken));
        }

        [HttpPost("{code}/publish")]
        public async Task<ActionResult<ServiceDefinitionDetailsResponse>>
            Publish(
                string code,
                CancellationToken cancellationToken)
        {
            var service = await db.ServiceDefinitions
                .SingleOrDefaultAsync(
                    item => item.code == code,
                    cancellationToken);

            if (service is null)
            {
                return NotFound(ServiceNotFound());
            }

            var errors = schemaValidator.ValidateForPublication(
                service.form_schema.RootElement);

            if (errors.Count > 0)
            {
                return SchemaValidationFailure(errors);
            }

            var registry = await dmsRegistryTypes.GetAsync(
                service.registry_type_code,
                cancellationToken);

            if (registry.Status == RegistryTypeLookupStatus.Unavailable)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    ApiProblemDetails.Create(
                        HttpContext,
                        StatusCodes.Status503ServiceUnavailable,
                        "DMS indisponibil",
                        "Registrul nu a putut fi validat în acest moment."));
            }

            if (registry.Status == RegistryTypeLookupStatus.NotFound)
            {
                return RegistryTypeValidationFailure(
                    "Codul registrului nu există în DMS.");
            }

            if (registry.IsClosed)
            {
                return RegistryTypeValidationFailure(
                    "Registrul selectat este închis în DMS.");
            }

            service.is_published = true;
            await db.SaveChangesAsync(cancellationToken);

            return Ok(await ToDetailsAsync(service, cancellationToken));
        }

        [HttpPost("{code}/unpublish")]
        public async Task<ActionResult<ServiceDefinitionDetailsResponse>>
            Unpublish(
                string code,
                CancellationToken cancellationToken)
        {
            var service = await db.ServiceDefinitions
                .SingleOrDefaultAsync(
                    item => item.code == code,
                    cancellationToken);

            if (service is null)
            {
                return NotFound(ServiceNotFound());
            }

            service.is_published = false;
            await db.SaveChangesAsync(cancellationToken);

            return Ok(await ToDetailsAsync(service, cancellationToken));
        }

        [HttpDelete("{code}")]
        public async Task<IActionResult> Delete(
            string code,
            CancellationToken cancellationToken)
        {
            var service = await db.ServiceDefinitions
                .SingleOrDefaultAsync(
                    item => item.code == code,
                    cancellationToken);

            if (service is null)
            {
                return NotFound(ServiceNotFound());
            }

            var hasSubmissions = await db.Submissions.AnyAsync(
                submission => submission.service_id == service.id,
                cancellationToken);

            if (hasSubmissions)
            {
                return Conflict(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status409Conflict,
                    "Serviciul are cereri",
                    "Un serviciu cu cereri depuse nu poate fi șters."));
            }

            db.ServiceDefinitions.Remove(service);
            await db.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        private async Task<ServiceDefinitionDetailsResponse>
            ToDetailsAsync(
                ServiceDefinition service,
                CancellationToken cancellationToken)
        {
            var submissionCount = await db.Submissions.CountAsync(
                submission => submission.service_id == service.id,
                cancellationToken);

            return new ServiceDefinitionDetailsResponse(
                service.id,
                service.code,
                service.title,
                service.short_description,
                service.description,
                service.registry_type_code,
                service.form_schema.RootElement.Clone(),
                service.schema_version,
                service.requires_attachment,
                service.max_attachments,
                service.is_published,
                service.display_order,
                submissionCount,
                service.created_at,
                service.updated_at);
        }

        private ActionResult ValidationFailure(
            IReadOnlyDictionary<string, string[]> errors)
        {
            return UnprocessableEntity(
                ApiProblemDetails.CreateValidation(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Date de serviciu invalide",
                    "Unul sau mai multe câmpuri sunt invalide.",
                    errors.ToDictionary(
                        item => item.Key,
                        item => item.Value)));
        }

        private ActionResult SchemaValidationFailure(
            IReadOnlyDictionary<string, string[]> errors)
        {
            return UnprocessableEntity(
                ApiProblemDetails.CreateValidation(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Schema formularului este invalidă",
                    "Serviciul nu poate fi publicat până când schema nu este corectă.",
                    errors.ToDictionary(
                        item => item.Key,
                        item => item.Value)));
        }

        private ActionResult RegistryTypeValidationFailure(string message)
        {
            return UnprocessableEntity(
                ApiProblemDetails.CreateValidation(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Registru DMS invalid",
                    "Serviciul nu poate fi publicat cu registrul selectat.",
                    new Dictionary<string, string[]>
                    {
                        ["registryTypeCode"] = [message]
                    }));
        }

        private static IReadOnlyDictionary<string, string[]>
            ValidateWriteRequest(
                string code,
                string title,
                string registryTypeCode,
                JsonElement? formSchema,
                bool requiresAttachment,
                int maxAttachments)
        {
            var errors = new Dictionary<string, string[]>();

            if (!CodePattern.IsMatch(code))
            {
                errors["code"] =
                [
                    "Codul poate conține litere mici, cifre și cratimă."
                ];
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                errors["title"] = ["Titlul este obligatoriu."];
            }

            if (string.IsNullOrWhiteSpace(registryTypeCode))
            {
                errors["registryTypeCode"] =
                [
                    "Codul registrului este obligatoriu."
                ];
            }

            if (formSchema is null ||
                formSchema.Value.ValueKind != JsonValueKind.Object)
            {
                errors["formSchema"] =
                [
                    "Schema formularului trebuie să fie un obiect JSON."
                ];
            }

            if (requiresAttachment && maxAttachments == 0)
            {
                errors["maxAttachments"] =
                [
                    "Un serviciu care cere atașamente trebuie să permită cel puțin unul."
                ];
            }

            return errors;
        }

        private static IReadOnlyDictionary<string, string[]> ValidatePage(
            int page,
            int pageSize,
            string? sort)
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

            if (!string.IsNullOrWhiteSpace(sort) &&
                !IsSupportedSort(sort))
            {
                errors["sort"] =
                [
                    "Sortarea acceptă displayOrder, title, code sau createdAt, cu asc/desc."
                ];
            }

            return errors;
        }

        private static bool IsSupportedSort(string sort)
        {
            var parts = sort.Split(':', StringSplitOptions.TrimEntries);

            return parts.Length == 2 &&
                parts[0] is "displayOrder" or "title" or "code" or
                    "createdAt" &&
                parts[1] is "asc" or "desc";
        }

        private static IOrderedQueryable<ServiceDefinition> ApplySort(
            IQueryable<ServiceDefinition> query,
            string? sort)
        {
            var parts = (sort ?? "displayOrder:asc")
                .Split(':', StringSplitOptions.TrimEntries);
            var descending = parts.Length == 2 && parts[1] == "desc";

            return parts[0] switch
            {
                "title" => descending
                    ? query.OrderByDescending(item => item.title)
                    : query.OrderBy(item => item.title),
                "code" => descending
                    ? query.OrderByDescending(item => item.code)
                    : query.OrderBy(item => item.code),
                "createdAt" => descending
                    ? query.OrderByDescending(item => item.created_at)
                    : query.OrderBy(item => item.created_at),
                _ => descending
                    ? query.OrderByDescending(item => item.display_order)
                    : query.OrderBy(item => item.display_order)
            };
        }

        private ProblemDetails ServiceNotFound()
        {
            return ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status404NotFound,
                "Serviciu inexistent",
                "Definiția serviciului nu există.");
        }

        private ProblemDetails DuplicateCode()
        {
            return ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status409Conflict,
                "Cod de serviciu duplicat",
                "Există deja un serviciu cu acest cod.");
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
