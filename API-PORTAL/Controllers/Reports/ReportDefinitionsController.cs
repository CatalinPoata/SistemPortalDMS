using API_PORTAL.Data;
using API_PORTAL.DTO.Reports;
using API_PORTAL.Entities;
using API_PORTAL.Errors;
using API_PORTAL.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.Reporting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace API_PORTAL.Controllers.Reports;

[ApiController]
[Route("api/report-definitions")]
[Authorize(Roles = "Admin")]
public sealed class ReportDefinitionsController : ControllerBase
{
    private static readonly Regex CodePattern = new(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.CultureInvariant);

    private readonly PortalDbContext db;
    private readonly PortalReportDefinitionValidator validator;
    private readonly PortalReportPreviewService previewService;
    private readonly IPdfRenderer pdfRenderer;

    public ReportDefinitionsController(
        PortalDbContext db,
        PortalReportDefinitionValidator validator,
        PortalReportPreviewService previewService,
        IPdfRenderer pdfRenderer)
    {
        this.db = db;
        this.validator = validator;
        this.previewService = previewService;
        this.pdfRenderer = pdfRenderer;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReportDefinitionSummaryResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var reports = await db.ReportDefinitions.AsNoTracking()
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
    public ActionResult<IReadOnlyList<ReportDatasetResponse>> GetDatasets() => Ok(
        PortalReportDatasets.All.Select(dataset => new ReportDatasetResponse(
            dataset.Key,
            dataset.Label,
            dataset.Fields.Select(field => new ReportDatasetFieldResponse(
                field.Key,
                field.Label,
                field.Type,
                field.IsNumeric)).ToList(),
            dataset.Parameters.Select(parameter => new ReportDatasetParameterResponse(
                parameter.Name,
                parameter.Label,
                parameter.Type,
                parameter.Source)).ToList())).ToList());

    [HttpGet("{code}/preview")]
    public async Task<ActionResult<ReportPreviewResponse>> Preview(
        string code,
        CancellationToken cancellationToken)
    {
        var report = await FindReadOnlyAsync(code, cancellationToken);
        if (report is null)
        {
            return ReportNotFound();
        }

        try
        {
            return Ok(await previewService.PreviewAsync(report, Request.Query, cancellationToken));
        }
        catch (PortalReportPreviewValidationException exception)
        {
            return ValidationFailure(exception.Errors);
        }
    }

    [HttpPost("preview")]
    public async Task<ActionResult<ReportPreviewResponse>> PreviewDraft(
        PreviewReportDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var datasetKey = request.DatasetKey?.Trim() ?? string.Empty;
        if (request.Definition is null || string.IsNullOrWhiteSpace(datasetKey))
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(datasetKey)) errors["datasetKey"] = ["Setul de date este obligatoriu."];
            if (request.Definition is null) errors["definition"] = ["Definiția raportului este obligatorie."];
            return ValidationFailure(errors);
        }

        var definitionErrors = validator.Validate(datasetKey, request.Definition.Value);
        if (definitionErrors.Count > 0)
        {
            return ValidationFailure(definitionErrors);
        }

        var draft = new ReportDefinition
        {
            code = "preview",
            name = "Previzualizare nesalvată",
            dataset_key = datasetKey,
            definition = JsonDocument.Parse(request.Definition.Value.GetRawText())
        };

        try
        {
            return Ok(await previewService.PreviewAsync(draft, Request.Query, cancellationToken));
        }
        catch (PortalReportPreviewValidationException exception)
        {
            return ValidationFailure(exception.Errors);
        }
    }

    [HttpPost("{code}/export")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [Produces("application/pdf")]
    public async Task<IActionResult> Export(
        string code,
        CancellationToken cancellationToken)
    {
        var report = await FindReadOnlyAsync(code, cancellationToken);
        if (report is null)
        {
            return ReportNotFound();
        }

        try
        {
            var preview = await previewService.ExportAsync(report, Request.Query, cancellationToken);
            var document = PortalReportHtmlDocument.Create(report, preview, Request.Query);
            var pdf = await pdfRenderer.RenderAsync(
                document.Html,
                new PdfRenderOptions(document.ShowPageNumbers),
                cancellationToken);
            return File(pdf, "application/pdf", $"{report.code}.pdf");
        }
        catch (PortalReportPreviewValidationException exception)
        {
            return ValidationFailure(exception.Errors);
        }
        catch (TimeoutException)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Generarea PDF este temporar indisponibilă",
                detail: "Motorul este ocupat sau randarea a durat prea mult. Reîncearcă.");
        }
    }

    [HttpGet("{code}/export.csv")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportCsv(
        string code,
        CancellationToken cancellationToken)
    {
        var report = await FindReadOnlyAsync(code, cancellationToken);
        if (report is null) return ReportNotFound();

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
        catch (PortalReportPreviewValidationException exception)
        {
            return ValidationFailure(exception.Errors);
        }
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<ReportDefinitionDetailsResponse>> GetByCode(
        string code,
        CancellationToken cancellationToken)
    {
        var report = await FindReadOnlyAsync(code, cancellationToken);
        return report is null ? ReportNotFound() : Ok(ToDetails(report));
    }

    [HttpPost]
    public async Task<ActionResult<ReportDefinitionDetailsResponse>> Create(
        CreateReportDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var code = request.Code?.Trim().ToLowerInvariant() ?? string.Empty;
        var name = request.Name?.Trim() ?? string.Empty;
        var datasetKey = request.DatasetKey?.Trim() ?? string.Empty;
        var errors = ValidateRequest(code, name, datasetKey, request.Definition, creating: true);
        if (errors.Count > 0) return ValidationFailure(errors);

        if (await db.ReportDefinitions.AnyAsync(item => item.code == code, cancellationToken))
        {
            return DuplicateCode();
        }

        var definitionErrors = validator.Validate(datasetKey, request.Definition!.Value);
        if (definitionErrors.Count > 0) return ValidationFailure(definitionErrors);

        var report = new ReportDefinition
        {
            id = Guid.NewGuid(),
            code = code,
            name = name,
            dataset_key = datasetKey,
            definition = JsonDocument.Parse(request.Definition.Value.GetRawText()),
            version = 1,
            is_system = false,
            updated_by_user_id = userId
        };
        db.ReportDefinitions.Add(report);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return DuplicateCode();
        }

        return CreatedAtAction(nameof(GetByCode), new { code = report.code }, ToDetails(report));
    }

    [HttpPut("{code}")]
    public async Task<ActionResult<ReportDefinitionDetailsResponse>> Update(
        string code,
        UpdateReportDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var report = await db.ReportDefinitions.SingleOrDefaultAsync(item => item.code == code, cancellationToken);
        if (report is null) return ReportNotFound();

        if (report.version != request.ExpectedVersion)
        {
            return Conflict(ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status409Conflict,
                "Definiție modificată între timp",
                "Reîncarcă raportul înainte de a salva modificările."));
        }

        var name = request.Name?.Trim() ?? string.Empty;
        var datasetKey = request.DatasetKey?.Trim() ?? string.Empty;
        var errors = ValidateRequest(report.code, name, datasetKey, request.Definition, creating: false);
        if (errors.Count > 0) return ValidationFailure(errors);

        var definitionErrors = validator.Validate(datasetKey, request.Definition!.Value);
        if (definitionErrors.Count > 0) return ValidationFailure(definitionErrors);

        report.name = name;
        report.dataset_key = datasetKey;
        report.definition = JsonDocument.Parse(request.Definition.Value.GetRawText());
        report.version++;
        report.updated_by_user_id = userId;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToDetails(report));
    }

    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code, CancellationToken cancellationToken)
    {
        var report = await db.ReportDefinitions.SingleOrDefaultAsync(item => item.code == code, cancellationToken);
        if (report is null) return ReportNotFound();
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

    private async Task<ReportDefinition?> FindReadOnlyAsync(string code, CancellationToken cancellationToken) =>
        await db.ReportDefinitions.AsNoTracking().SingleOrDefaultAsync(item => item.code == code, cancellationToken);

    private ActionResult ValidationFailure(IReadOnlyDictionary<string, string[]> errors) => UnprocessableEntity(
        ApiProblemDetails.CreateValidation(
            HttpContext,
            StatusCodes.Status422UnprocessableEntity,
            "Definiție de raport invalidă",
            "Definiția nu respectă regulile motorului de rapoarte.",
            errors.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal)));

    private NotFoundObjectResult ReportNotFound() => NotFound(
        ApiProblemDetails.Create(
            HttpContext,
            StatusCodes.Status404NotFound,
            "Raport inexistent",
            "Definiția de raport nu există."));

    private ActionResult<ReportDefinitionDetailsResponse> DuplicateCode() => Conflict(
        ApiProblemDetails.Create(
            HttpContext,
            StatusCodes.Status409Conflict,
            "Cod de raport duplicat",
            "Există deja o definiție de raport cu acest cod."));

    private static IReadOnlyDictionary<string, string[]> ValidateRequest(
        string code,
        string name,
        string datasetKey,
        JsonElement? definition,
        bool creating)
    {
        var errors = new Dictionary<string, string[]>();
        if (creating && !CodePattern.IsMatch(code)) errors["code"] = ["Codul poate conține litere mici, cifre și cratimă."];
        if (string.IsNullOrWhiteSpace(name)) errors["name"] = ["Numele raportului este obligatoriu."];
        if (string.IsNullOrWhiteSpace(datasetKey)) errors["datasetKey"] = ["Setul de date este obligatoriu."];
        if (definition is null) errors["definition"] = ["Definiția raportului este obligatorie."];
        return errors;
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub),
        out userId);

    private static ReportDefinitionDetailsResponse ToDetails(ReportDefinition report) => new(
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
