using API_DMS.Data;
using API_DMS.DTO.Reports;
using API_DMS.Entities;
using API_DMS.Errors;
using API_DMS.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.Reporting;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace API_DMS.Controllers.Reports
{
    [ApiController]
    [Route("api/report-definitions")]
    [Authorize(Roles = "Clerk,Admin")]
    public sealed class ReportDefinitionsController : ControllerBase
    {
        private static readonly Regex CodePattern =
            new(
                "^[a-z0-9]+(?:-[a-z0-9]+)*$",
                RegexOptions.CultureInvariant);

        private readonly DmsDbContext db;
        private readonly DmsReportDefinitionValidator validator;
        private readonly DmsReportPreviewService previewService;
        private readonly IPdfRenderer pdfRenderer;

        public ReportDefinitionsController(
            DmsDbContext db,
            DmsReportDefinitionValidator validator,
            DmsReportPreviewService previewService,
            IPdfRenderer pdfRenderer)
        {
            this.db = db;
            this.validator = validator;
            this.previewService = previewService;
            this.pdfRenderer = pdfRenderer;
        }

        [HttpGet]
        public async Task<ActionResult<
            IReadOnlyList<ReportDefinitionSummaryResponse>>> GetAll(
            CancellationToken cancellationToken)
        {
            var reports = await db.ReportDefinitions
                .AsNoTracking()
                .OrderBy(report => report.code)
                .Select(report => new ReportDefinitionSummaryResponse(
                    report.id,
                    report.code,
                    report.name,
                    report.dataset_key,
                    report.version,
                    report.is_system,
                    report.created_at,
                    report.updated_at))
                .ToListAsync(cancellationToken);

            return Ok(reports);
        }

        [HttpGet("datasets")]
        public ActionResult<IReadOnlyList<ReportDatasetResponse>> GetDatasets()
        {
            var datasets = DmsReportDatasets.All
                .Select(dataset => new ReportDatasetResponse(
                    dataset.Key,
                    dataset.Label,
                    dataset.Fields
                        .Select(field => new ReportDatasetFieldResponse(
                            field.Key,
                            field.Label,
                            field.Type,
                            field.IsNumeric))
                        .ToList(),
                    dataset.Parameters
                        .Select(parameter =>
                            new ReportDatasetParameterResponse(
                                parameter.Name,
                                parameter.Label,
                                parameter.Type,
                                parameter.Source))
                        .ToList()))
                .ToList();

            return Ok(datasets);
        }

        [HttpGet("{code}/preview")]
        public async Task<ActionResult<ReportPreviewResponse>> Preview(
            string code,
            CancellationToken cancellationToken)
        {
            var report = await db.ReportDefinitions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.code == code,
                    cancellationToken);

            if (report is null)
            {
                return NotFound(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    "Raport inexistent",
                    "Definiția de raport nu există."));
            }

            try
            {
                var preview = await previewService.PreviewAsync(
                    report,
                    Request.Query,
                    cancellationToken);

                return Ok(preview);
            }
            catch (ReportPreviewValidationException exception)
            {
                return ValidationFailure(exception.Errors);
            }
        }

        [HttpPost("preview")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ReportPreviewResponse>> PreviewDraft(
            PreviewReportDefinitionRequest request,
            CancellationToken cancellationToken)
        {
            var datasetKey = request.DatasetKey?.Trim() ?? "";
            var errors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(datasetKey))
            {
                errors["datasetKey"] =
                [
                    "Setul de date este obligatoriu."
                ];
            }

            if (request.Definition is null)
            {
                errors["definition"] =
                [
                    "Definiția raportului este obligatorie."
                ];
            }

            if (errors.Count > 0)
            {
                return ValidationFailure(errors);
            }

            var definitionErrors = validator.Validate(
                datasetKey,
                request.Definition!.Value);

            if (definitionErrors.Count > 0)
            {
                return ValidationFailure(definitionErrors);
            }

            var draft = new ReportDefinition
            {
                code = "preview",
                name = "Previzualizare nesalvată",
                dataset_key = datasetKey,
                definition = JsonDocument.Parse(
                    request.Definition.Value.GetRawText())
            };

            try
            {
                var preview = await previewService.PreviewAsync(
                    draft,
                    Request.Query,
                    cancellationToken);

                return Ok(preview);
            }
            catch (ReportPreviewValidationException exception)
            {
                return ValidationFailure(exception.Errors);
            }
        }

        [HttpPost("{code}/export")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        [Produces("application/pdf")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Export(
            string code,
            CancellationToken cancellationToken)
        {
            var report = await db.ReportDefinitions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.code == code,
                    cancellationToken);

            if (report is null)
            {
                return NotFound(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    "Raport inexistent",
                    "Definiția de raport nu există."));
            }

            try
            {
                var preview = await previewService.ExportAsync(
                    report,
                    Request.Query,
                    cancellationToken);

                var document = DmsReportHtmlDocument.Create(
                    report,
                    preview,
                    Request.Query);

                var pdf = await pdfRenderer.RenderAsync(
                    document.Html,
                    new PdfRenderOptions(
                        document.ShowPageNumbers),
                    cancellationToken);

                return File(
                    pdf,
                    "application/pdf",
                    $"{report.code}.pdf");
            }
            catch (ReportPreviewValidationException exception)
            {
                return ValidationFailure(exception.Errors);
            }
            catch (TimeoutException)
            {
                return Problem(
                    statusCode:
                        StatusCodes.Status503ServiceUnavailable,
                    title:
                        "Generarea PDF este temporar indisponibilă",
                    detail:
                        "Motorul este ocupat sau randarea a durat prea mult. Reîncearcă.");
            }
        }

        [HttpGet("{code}/export.csv")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        [Produces("text/csv")]
        public async Task<IActionResult> ExportCsv(
            string code,
            CancellationToken cancellationToken)
        {
            var report = await db.ReportDefinitions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.code == code,
                    cancellationToken);

            if (report is null)
            {
                return NotFound(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    "Raport inexistent",
                    "Definiția de raport nu există."));
            }

            try
            {
                var preview = await previewService.ExportAsync(
                    report, Request.Query, cancellationToken);
                var csv = CsvReportDocument.Create(
                    preview.Columns,
                    preview.Rows,
                    column => column.Field,
                    column => column.Label);
                return File(csv, "text/csv; charset=utf-8", $"{report.code}.csv");
            }
            catch (ReportPreviewValidationException exception)
            {
                return ValidationFailure(exception.Errors);
            }
        }

        [HttpGet("{code}")]
        public async Task<ActionResult<ReportDefinitionDetailsResponse>> GetByCode(
            string code,
            CancellationToken cancellationToken)
        {
            var report = await db.ReportDefinitions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.code == code,
                    cancellationToken);

            if (report is null)
            {
                return NotFound(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    "Raport inexistent",
                    "Definiția de raport nu există."));
            }

            return Ok(ToDetails(report));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ReportDefinitionDetailsResponse>> Create(
            CreateReportDefinitionRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var code = request.Code?.Trim().ToLowerInvariant() ?? "";
            var name = request.Name?.Trim() ?? "";
            var datasetKey = request.DatasetKey?.Trim() ?? "";

            var errors = ValidateRequest(
                code,
                name,
                datasetKey,
                request.Definition);

            if (errors.Count > 0)
            {
                return ValidationFailure(errors);
            }

            if (await db.ReportDefinitions.AnyAsync(
                    report => report.code == code,
                    cancellationToken))
            {
                return Conflict(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status409Conflict,
                    "Cod de raport duplicat",
                    "Există deja o definiție de raport cu acest cod."));
            }

            var definitionErrors = validator.Validate(
                datasetKey,
                request.Definition!.Value);

            if (definitionErrors.Count > 0)
            {
                return ValidationFailure(definitionErrors);
            }

            var report = new ReportDefinition
            {
                id = Guid.NewGuid(),
                code = code,
                name = name,
                dataset_key = datasetKey,
                definition = JsonDocument.Parse(
                    request.Definition.Value.GetRawText()),
                version = 1,
                is_system = false,
                updated_by_user_id = userId
            };

            db.ReportDefinitions.Add(report);

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
                    "Cod de raport duplicat",
                    "Există deja o definiție de raport cu acest cod."));
            }

            return CreatedAtAction(
                nameof(GetByCode),
                new { code = report.code },
                ToDetails(report));
        }

        [HttpPut("{code}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ReportDefinitionDetailsResponse>> Update(
            string code,
            UpdateReportDefinitionRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var report = await db.ReportDefinitions
                .SingleOrDefaultAsync(
                    item => item.code == code,
                    cancellationToken);

            if (report is null)
            {
                return NotFound(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    "Raport inexistent",
                    "Definiția de raport nu există."));
            }

            if (report.version != request.ExpectedVersion)
            {
                return Conflict(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status409Conflict,
                    "Definiție modificată între timp",
                    "Reîncarcă raportul înainte de a salva modificările."));
            }

            var name = request.Name?.Trim() ?? "";
            var datasetKey = request.DatasetKey?.Trim() ?? "";

            var errors = ValidateRequest(
                report.code,
                name,
                datasetKey,
                request.Definition);

            if (errors.Count > 0)
            {
                return ValidationFailure(errors);
            }

            var definitionErrors = validator.Validate(
                datasetKey,
                request.Definition!.Value);

            if (definitionErrors.Count > 0)
            {
                return ValidationFailure(definitionErrors);
            }

            report.name = name;
            report.dataset_key = datasetKey;
            report.definition = JsonDocument.Parse(
                request.Definition.Value.GetRawText());
            report.version++;
            report.updated_by_user_id = userId;

            await db.SaveChangesAsync(cancellationToken);

            return Ok(ToDetails(report));
        }

        [HttpDelete("{code}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(
            string code,
            CancellationToken cancellationToken)
        {
            var report = await db.ReportDefinitions
                .SingleOrDefaultAsync(
                    item => item.code == code,
                    cancellationToken);

            if (report is null)
            {
                return NotFound(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    "Raport inexistent",
                    "Definiția de raport nu există."));
            }

            if (report.is_system)
            {
                return Conflict(ApiProblemDetails.Create(
                    HttpContext,
                    StatusCodes.Status409Conflict,
                    "Raport de sistem protejat",
                    "Raporturile de sistem nu pot fi șterse."));
            }

            db.ReportDefinitions.Remove(report);
            await db.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        private ActionResult ValidationFailure(
            IReadOnlyDictionary<string, string[]> errors)
        {
            return UnprocessableEntity(
                ApiProblemDetails.CreateValidation(
                    HttpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    "Definiție de raport invalidă",
                    "Definiția nu respectă regulile motorului de rapoarte.",
                    errors.ToDictionary(
                        item => item.Key,
                        item => item.Value)));
        }

        private static IReadOnlyDictionary<string, string[]> ValidateRequest(
            string code,
            string name,
            string datasetKey,
            JsonElement? definition)
        {
            var errors = new Dictionary<string, string[]>();

            if (!CodePattern.IsMatch(code))
            {
                errors["code"] =
                [
                    "Codul poate conține litere mici, cifre și cratimă."
                ];
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                errors["name"] =
                [
                    "Numele raportului este obligatoriu."
                ];
            }

            if (string.IsNullOrWhiteSpace(datasetKey))
            {
                errors["datasetKey"] =
                [
                    "Setul de date este obligatoriu."
                ];
            }

            if (definition is null)
            {
                errors["definition"] =
                [
                    "Definiția raportului este obligatorie."
                ];
            }

            return errors;
        }

        private bool TryGetUserId(out Guid userId)
        {
            var value = User.FindFirstValue(
                JwtRegisteredClaimNames.Sub);

            return Guid.TryParse(value, out userId);
        }

        private static ReportDefinitionDetailsResponse ToDetails(
            ReportDefinition report)
        {
            return new ReportDefinitionDetailsResponse(
                report.id,
                report.code,
                report.name,
                report.dataset_key,
                report.definition.RootElement.Clone(),
                report.version,
                report.is_system,
                report.created_at,
                report.updated_at);
        }
    }
}
