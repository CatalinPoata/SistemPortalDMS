using API_PORTAL.Data;
using API_PORTAL.DTO.Surveys;
using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using API_PORTAL.Errors;
using API_PORTAL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using SurveyResponseDto = API_PORTAL.DTO.Surveys.SurveyResponse;

namespace API_PORTAL.Controllers.Surveys;

[ApiController]
[Route("api/surveys")]
[Authorize(Roles = nameof(Role.Admin))]
public sealed class SurveysController : ControllerBase
{
    private static readonly Regex CodePattern = new(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.CultureInvariant);

    private readonly PortalDbContext db;
    private readonly IServiceHtmlSanitizer htmlSanitizer;

    public SurveysController(PortalDbContext db, IServiceHtmlSanitizer htmlSanitizer)
    {
        this.db = db;
        this.htmlSanitizer = htmlSanitizer;
    }

    [HttpGet]
    public async Task<ActionResult<PagedSurveyResponse>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return InvalidPage(page, pageSize);
        }

        var query = db.Surveys.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new SurveyListItemResponse(
                item.id,
                item.code,
                item.title,
                item.description,
                item.starts_at,
                item.ends_at,
                item.allow_anonymous,
                item.show_results,
                item.is_published,
                db.SurveyQuestions.Count(question => question.survey_id == item.id),
                db.SurveyResponses.Count(response => response.survey_id == item.id)))
            .ToListAsync(cancellationToken);

        return Ok(new PagedSurveyResponse(items, page, pageSize, total));
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<SurveyResponseDto>> GetByCode(
        string code,
        CancellationToken cancellationToken)
    {
        var survey = await FindAsync(code, cancellationToken);
        return survey is null
            ? NotFound(NotFoundProblem())
            : Ok(await ToResponseAsync(survey, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<SurveyResponseDto>> Create(
        CreateSurveyRequest request,
        CancellationToken cancellationToken)
    {
        var code = request.Code?.Trim().ToLowerInvariant() ?? string.Empty;
        var errors = ValidateSurvey(code, request.Title, request.StartsAt, request.EndsAt);
        if (errors.Count > 0)
        {
            return ValidationFailure(errors);
        }

        if (await db.Surveys.AnyAsync(item => item.code == code, cancellationToken))
        {
            return Conflict(DuplicateCode());
        }

        var survey = new Survey
        {
            id = Guid.NewGuid(),
            code = code,
            title = request.Title!.Trim(),
            description = htmlSanitizer.Sanitize(request.Description),
            starts_at = request.StartsAt?.ToUniversalTime(),
            ends_at = request.EndsAt?.ToUniversalTime(),
            allow_anonymous = request.AllowAnonymous,
            show_results = request.ShowResults,
            is_published = false
        };

        db.Surveys.Add(survey);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return Conflict(DuplicateCode());
        }

        return CreatedAtAction(
            nameof(GetByCode),
            new { code = survey.code },
            await ToResponseAsync(survey, cancellationToken));
    }

    [HttpPut("{code}")]
    public async Task<ActionResult<SurveyResponseDto>> Update(
        string code,
        UpdateSurveyRequest request,
        CancellationToken cancellationToken)
    {
        var survey = await FindAsync(code, cancellationToken);
        if (survey is null)
        {
            return NotFound(NotFoundProblem());
        }

        var errors = ValidateSurvey(survey.code, request.Title, request.StartsAt, request.EndsAt);
        if (errors.Count > 0)
        {
            return ValidationFailure(errors);
        }

        survey.title = request.Title!.Trim();
        survey.description = htmlSanitizer.Sanitize(request.Description);
        survey.starts_at = request.StartsAt?.ToUniversalTime();
        survey.ends_at = request.EndsAt?.ToUniversalTime();
        survey.allow_anonymous = request.AllowAnonymous;
        survey.show_results = request.ShowResults;
        survey.is_published = request.IsPublished;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(await ToResponseAsync(survey, cancellationToken));
    }

    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code, CancellationToken cancellationToken)
    {
        var survey = await FindAsync(code, cancellationToken);
        if (survey is null)
        {
            return NotFound(NotFoundProblem());
        }

        if (await db.SurveyResponses.AnyAsync(item => item.survey_id == survey.id, cancellationToken))
        {
            return Conflict(ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status409Conflict,
                "Chestionarul are răspunsuri",
                "Un chestionar cu răspunsuri nu poate fi șters."));
        }

        db.Surveys.Remove(survey);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{code}/publish")]
    public async Task<ActionResult<SurveyResponseDto>> Publish(
        string code,
        CancellationToken cancellationToken)
    {
        var survey = await FindAsync(code, cancellationToken);
        if (survey is null)
        {
            return NotFound(NotFoundProblem());
        }

        if (!await db.SurveyQuestions.AnyAsync(item => item.survey_id == survey.id, cancellationToken))
        {
            return UnprocessableEntity(ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status422UnprocessableEntity,
                "Chestionar fără întrebări",
                "Un chestionar trebuie să conțină cel puțin o întrebare înainte de publicare."));
        }

        survey.is_published = true;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await ToResponseAsync(survey, cancellationToken));
    }

    [HttpPost("{code}/unpublish")]
    public async Task<ActionResult<SurveyResponseDto>> Unpublish(
        string code,
        CancellationToken cancellationToken)
    {
        var survey = await FindAsync(code, cancellationToken);
        if (survey is null)
        {
            return NotFound(NotFoundProblem());
        }

        survey.is_published = false;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await ToResponseAsync(survey, cancellationToken));
    }

    [HttpPost("{code}/questions")]
    public async Task<ActionResult<SurveyResponseDto>> AddQuestion(
        string code,
        UpsertSurveyQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var survey = await FindAsync(code, cancellationToken);
        if (survey is null)
        {
            return NotFound(NotFoundProblem());
        }

        var questionErrors = ValidateQuestion(request);
        if (questionErrors.Count > 0)
        {
            return ValidationFailure(questionErrors);
        }

        var key = request.Key!.Trim();
        if (await db.SurveyQuestions.AnyAsync(
                item => item.survey_id == survey.id && item.key == key,
                cancellationToken))
        {
            return Conflict(ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status409Conflict,
                "Cheie duplicată",
                "Există deja o întrebare cu această cheie în chestionar."));
        }

        db.SurveyQuestions.Add(ToEntity(survey.id, request, key));
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await ToResponseAsync(survey, cancellationToken));
    }

    [HttpPut("{code}/questions/{key}")]
    public async Task<ActionResult<SurveyResponseDto>> UpdateQuestion(
        string code,
        string key,
        UpsertSurveyQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var survey = await FindAsync(code, cancellationToken);
        var question = survey is null
            ? null
            : await db.SurveyQuestions.SingleOrDefaultAsync(
                item => item.survey_id == survey.id && item.key == key,
                cancellationToken);

        if (survey is null || question is null)
        {
            return NotFound(NotFoundProblem("Întrebarea nu există."));
        }

        var errors = ValidateQuestion(request);
        if (errors.Count > 0)
        {
            return ValidationFailure(errors);
        }

        var hasResponses = await db.SurveyResponses.AnyAsync(
            item => item.survey_id == survey.id,
            cancellationToken);

        if (hasResponses &&
            (request.Key!.Trim() != question.key ||
             request.Type != question.type ||
             request.DisplayOrder != question.display_order ||
             !JsonEquals(request.Options, question.options)))
        {
            return UnprocessableEntity(ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status422UnprocessableEntity,
                "Întrebare blocată",
                "După primul răspuns se poate modifica doar textul întrebării."));
        }

        question.text = request.Text!.Trim();
        if (!hasResponses)
        {
            question.key = request.Key!.Trim();
            question.type = request.Type;
            question.options = ToDocument(request.Options);
            question.display_order = request.DisplayOrder;
        }

        question.is_required = request.IsRequired;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await ToResponseAsync(survey, cancellationToken));
    }

    [HttpDelete("{code}/questions/{key}")]
    public async Task<IActionResult> DeleteQuestion(
        string code,
        string key,
        CancellationToken cancellationToken)
    {
        var survey = await FindAsync(code, cancellationToken);
        if (survey is null)
        {
            return NotFound(NotFoundProblem());
        }

        var question = await db.SurveyQuestions.SingleOrDefaultAsync(
            item => item.survey_id == survey.id && item.key == key,
            cancellationToken);
        if (question is null)
        {
            return NotFound(NotFoundProblem("Întrebarea nu există."));
        }

        if (await db.SurveyResponses.AnyAsync(item => item.survey_id == survey.id, cancellationToken))
        {
            return UnprocessableEntity(ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status422UnprocessableEntity,
                "Întrebare blocată",
                "Întrebările nu pot fi șterse după primul răspuns."));
        }

        db.SurveyQuestions.Remove(question);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{code}/results")]
    public async Task<ActionResult<SurveyResultsResponse>> Results(
        string code,
        CancellationToken cancellationToken)
    {
        var survey = await FindAsync(code, cancellationToken);
        if (survey is null)
        {
            return NotFound(NotFoundProblem());
        }

        return Ok(await BuildResultsAsync(survey, cancellationToken, includeResponses: true));
    }

    private async Task<Survey?> FindAsync(string code, CancellationToken cancellationToken) =>
        await db.Surveys.SingleOrDefaultAsync(
            item => item.code == code.Trim().ToLower(),
            cancellationToken);

    private async Task<SurveyResponseDto> ToResponseAsync(
        Survey survey,
        CancellationToken cancellationToken)
    {
        var questionEntities = await db.SurveyQuestions.AsNoTracking()
            .Where(item => item.survey_id == survey.id)
            .OrderBy(item => item.display_order)
            .ThenBy(item => item.key)
            .ToListAsync(cancellationToken);

        var questions = questionEntities
            .Select(item => new SurveyQuestionResponse(
                item.id,
                item.key,
                item.text,
                item.type,
                item.options == null ? null : item.options.RootElement.Clone(),
                item.is_required,
                item.display_order))
            .ToList();

        var userId = CurrentUserId();
        var hasResponded = userId.HasValue && await db.SurveyResponses.AnyAsync(
            item => item.survey_id == survey.id && item.user_id == userId,
            cancellationToken);

        return new SurveyResponseDto(
            survey.id,
            survey.code,
            survey.title,
            survey.description,
            survey.starts_at,
            survey.ends_at,
            survey.allow_anonymous,
            survey.show_results,
            survey.is_published,
            questions,
            hasResponded);
    }

    private async Task<SurveyResultsResponse> BuildResultsAsync(
        Survey survey,
        CancellationToken cancellationToken,
        bool includeResponses)
    {
        var questions = await db.SurveyQuestions.AsNoTracking()
            .Where(item => item.survey_id == survey.id)
            .OrderBy(item => item.display_order)
            .ToListAsync(cancellationToken);
        var responses = await db.SurveyResponses.AsNoTracking()
            .Where(item => item.survey_id == survey.id)
            .OrderBy(item => item.submitted_at)
            .ToListAsync(cancellationToken);

        var items = questions.Select(question =>
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var responseCount = 0;
            foreach (var response in responses)
            {
                if (!response.answers.RootElement.TryGetProperty(question.key, out var answer))
                {
                    continue;
                }

                responseCount++;
                if (answer.ValueKind == JsonValueKind.Array)
                {
                    foreach (var value in answer.EnumerateArray())
                    {
                        AddCount(counts, DisplayValue(value));
                    }
                }
                else
                {
                    AddCount(counts, DisplayValue(answer));
                }
            }

            return new SurveyResultItem(question.key, question.type, responseCount, counts);
        }).ToList();

        var summaries = includeResponses
            ? responses.Select(response => new SurveyResponseSummary(
                response.id,
                response.submitted_at,
                response.answers.RootElement.Clone())).ToList()
            : [];

        return new SurveyResultsResponse(
            survey.id,
            survey.code,
            responses.Count,
            items,
            summaries);
    }

    private Dictionary<string, string[]> ValidateSurvey(
        string code,
        string? title,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt)
    {
        var errors = new Dictionary<string, string[]>();
        if (!CodePattern.IsMatch(code))
        {
            errors["code"] = ["Codul poate conține litere mici, cifre și cratime."];
        }
        if (string.IsNullOrWhiteSpace(title))
        {
            errors["title"] = ["Titlul este obligatoriu."];
        }
        if (startsAt.HasValue && endsAt.HasValue && endsAt <= startsAt)
        {
            errors["endsAt"] = ["Data de sfârșit trebuie să fie după data de început."];
        }
        return errors;
    }

    private Dictionary<string, string[]> ValidateQuestion(UpsertSurveyQuestionRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Key)) errors["key"] = ["Cheia este obligatorie."];
        if (string.IsNullOrWhiteSpace(request.Text)) errors["text"] = ["Textul este obligatoriu."];
        if (request.DisplayOrder < 0) errors["displayOrder"] = ["Ordinea nu poate fi negativă."];

        if (request.Type == SurveyQuestionType.FreeText)
        {
            if (request.Options.HasValue && request.Options.Value.ValueKind != JsonValueKind.Null)
            {
                errors["options"] = ["Întrebările FreeText nu acceptă opțiuni."];
            }
        }
        else if (!request.Options.HasValue)
        {
            errors["options"] = ["Opțiunile sunt obligatorii pentru acest tip de întrebare."];
        }
        else if (request.Type is SurveyQuestionType.SingleChoice or SurveyQuestionType.MultiChoice)
        {
            if (!IsChoiceOptions(request.Options.Value))
            {
                errors["options"] = ["Opțiunile trebuie să fie o listă de obiecte value/label."];
            }
        }
        else if (request.Type == SurveyQuestionType.Rating && !IsRatingOptions(request.Options.Value))
        {
            errors["options"] = ["Rating-ul trebuie să declare min și max întregi, cu min < max."];
        }

        return errors;
    }

    private static bool IsChoiceOptions(JsonElement element) =>
        element.ValueKind == JsonValueKind.Array &&
        element.GetArrayLength() > 0 &&
        element.EnumerateArray().All(item =>
            item.ValueKind == JsonValueKind.Object &&
            item.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value.GetString()) &&
            item.TryGetProperty("label", out var label) && label.ValueKind == JsonValueKind.String);

    private static bool IsRatingOptions(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty("min", out var min) && min.TryGetInt32(out var minValue) &&
        element.TryGetProperty("max", out var max) && max.TryGetInt32(out var maxValue) &&
        minValue < maxValue;

    private static SurveyQuestion ToEntity(Guid surveyId, UpsertSurveyQuestionRequest request, string key) => new()
    {
        id = Guid.NewGuid(),
        survey_id = surveyId,
        key = key,
        text = request.Text!.Trim(),
        type = request.Type,
        options = ToDocument(request.Options),
        is_required = request.IsRequired,
        display_order = request.DisplayOrder
    };

    private static JsonDocument? ToDocument(JsonElement? value) =>
        value.HasValue && value.Value.ValueKind != JsonValueKind.Null
            ? JsonDocument.Parse(value.Value.GetRawText())
            : null;

    private static bool JsonEquals(JsonElement? request, JsonDocument? stored)
    {
        if (!request.HasValue || request.Value.ValueKind == JsonValueKind.Null)
        {
            return stored is null;
        }
        return stored is not null && request.Value.GetRawText() == stored.RootElement.GetRawText();
    }

    private Guid? CurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private ActionResult InvalidPage(int page, int pageSize) =>
        UnprocessableEntity(ApiProblemDetails.CreateValidation(
            HttpContext,
            StatusCodes.Status422UnprocessableEntity,
            "Paginare invalidă",
            "Parametrii de paginare sunt invalizi.",
            new Dictionary<string, string[]>
            {
                ["page"] = page < 1 ? ["Pagina trebuie să fie cel puțin 1."] : [],
                ["pageSize"] = pageSize is < 1 or > 100 ? ["pageSize trebuie să fie între 1 și 100."] : []
            }));

    private ActionResult ValidationFailure(Dictionary<string, string[]> errors) =>
        UnprocessableEntity(ApiProblemDetails.CreateValidation(
            HttpContext,
            StatusCodes.Status422UnprocessableEntity,
            "Date invalide",
            "Unul sau mai multe câmpuri sunt invalide.",
            errors));

    private ProblemDetails NotFoundProblem(string detail = "Chestionarul nu există.") =>
        ApiProblemDetails.Create(HttpContext, StatusCodes.Status404NotFound, "Chestionar inexistent", detail);

    private ProblemDetails DuplicateCode() =>
        ApiProblemDetails.Create(HttpContext, StatusCodes.Status409Conflict, "Cod duplicat", "Există deja un chestionar cu acest cod.");

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static void AddCount(Dictionary<string, int> counts, string value) =>
        counts[value] = counts.TryGetValue(value, out var count) ? count + 1 : 1;

    private static string DisplayValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => value.GetRawText()
    };
}
