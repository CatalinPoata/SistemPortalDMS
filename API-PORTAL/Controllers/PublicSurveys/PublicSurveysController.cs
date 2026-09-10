using API_PORTAL.Data;
using API_PORTAL.DTO.Surveys;
using API_PORTAL.Entities;
using API_PORTAL.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Claims;
using System.Text.Json;
using SurveyResponseDto = API_PORTAL.DTO.Surveys.SurveyResponse;

namespace API_PORTAL.Controllers.PublicSurveys;

[ApiController]
[Route("api/public/surveys")]
[AllowAnonymous]
public sealed class PublicSurveysController : ControllerBase
{
    private readonly PortalDbContext db;

    public PublicSurveysController(PortalDbContext db)
    {
        this.db = db;
    }

    [HttpGet]
    public async Task<ActionResult<PagedSurveyResponse>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return UnprocessableEntity(ApiProblemDetails.CreateValidation(
                HttpContext,
                StatusCodes.Status422UnprocessableEntity,
                "Paginare invalidă",
                "Parametrii de paginare sunt invalizi.",
                new Dictionary<string, string[]>
                {
                    ["page"] = page < 1 ? ["Pagina trebuie să fie cel puțin 1."] : [],
                    ["pageSize"] = pageSize is < 1 or > 100 ? ["pageSize trebuie să fie între 1 și 100."] : []
                }));
        }

        var now = DateTimeOffset.UtcNow;
        var query = db.Surveys.AsNoTracking().Where(item =>
            item.is_published &&
            (item.starts_at == null || item.starts_at <= now) &&
            (item.ends_at == null || item.ends_at > now));
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(item => item.title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new SurveyListItemResponse(
                item.id, item.code, item.title, item.description,
                item.starts_at, item.ends_at, item.allow_anonymous,
                item.show_results, item.is_published,
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
        var survey = await FindActiveAsync(code, cancellationToken);
        if (survey is null)
        {
            return NotFound(Problem("Chestionarul nu există sau nu este disponibil."));
        }

        return Ok(await ToResponseAsync(survey, cancellationToken));
    }

    [HttpPost("{code}/responses")]
    public async Task<ActionResult<SurveySubmissionResponse>> Submit(
        string code,
        SubmitSurveyResponseRequest request,
        CancellationToken cancellationToken)
    {
        var survey = await FindPublishedAsync(code, cancellationToken);
        if (survey is null)
        {
            return NotFound(Problem("Chestionarul nu există sau nu este publicat."));
        }

        var now = DateTimeOffset.UtcNow;
        if (survey.starts_at.HasValue && now < survey.starts_at ||
            survey.ends_at.HasValue && now >= survey.ends_at)
        {
            return UnprocessableEntity(ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status422UnprocessableEntity,
                "Chestionar indisponibil",
                "Perioada de răspuns a chestionarului nu este activă."));
        }

        var userId = CurrentUserId();
        if (!userId.HasValue && !survey.allow_anonymous)
        {
            return Unauthorized(ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status401Unauthorized,
                "Autentificare necesară",
                "Acest chestionar acceptă doar răspunsuri de la utilizatori autentificați."));
        }

        if (userId.HasValue && await db.SurveyResponses.AnyAsync(
                item => item.survey_id == survey.id && item.user_id == userId,
                cancellationToken))
        {
            return UnprocessableEntity(ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status422UnprocessableEntity,
                "Răspuns duplicat",
                "Utilizatorul a răspuns deja la acest chestionar."));
        }

        var questions = await db.SurveyQuestions.AsNoTracking()
            .Where(item => item.survey_id == survey.id)
            .OrderBy(item => item.display_order)
            .ToListAsync(cancellationToken);
        var errors = ValidateAnswers(questions, request.Answers);
        if (errors.Count > 0)
        {
            return UnprocessableEntity(ApiProblemDetails.CreateValidation(
                HttpContext,
                StatusCodes.Status422UnprocessableEntity,
                "Răspunsuri invalide",
                "Răspunsurile nu respectă întrebările chestionarului.",
                errors));
        }

        var response = new API_PORTAL.Entities.SurveyResponse
        {
            id = Guid.NewGuid(),
            survey_id = survey.id,
            user_id = userId,
            answers = JsonDocument.Parse(JsonSerializer.Serialize(request.Answers)),
            submitted_at = now
        };
        db.SurveyResponses.Add(response);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        })
        {
            return UnprocessableEntity(ApiProblemDetails.Create(
                HttpContext,
                StatusCodes.Status422UnprocessableEntity,
                "Răspuns duplicat",
                "Utilizatorul a răspuns deja la acest chestionar."));
        }

        var results = survey.show_results
            ? await BuildResultsAsync(survey, cancellationToken)
            : null;
        return CreatedAtAction(
            nameof(GetByCode),
            new { code = survey.code },
            new SurveySubmissionResponse(response.id, survey.code, survey.show_results, response.submitted_at, results));
    }

    private async Task<Survey?> FindActiveAsync(string code, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return await db.Surveys.AsNoTracking().SingleOrDefaultAsync(item =>
            item.code == code.Trim().ToLower() && item.is_published &&
            (item.starts_at == null || item.starts_at <= now) &&
            (item.ends_at == null || item.ends_at > now), cancellationToken);
    }

    private async Task<Survey?> FindPublishedAsync(string code, CancellationToken cancellationToken) =>
        await db.Surveys.SingleOrDefaultAsync(
            item => item.code == code.Trim().ToLower() && item.is_published,
            cancellationToken);

    private async Task<SurveyResponseDto> ToResponseAsync(Survey survey, CancellationToken cancellationToken)
    {
        var questionEntities = await db.SurveyQuestions.AsNoTracking()
            .Where(item => item.survey_id == survey.id)
            .OrderBy(item => item.display_order)
            .ThenBy(item => item.key)
            .ToListAsync(cancellationToken);

        var questions = questionEntities
            .Select(item => new SurveyQuestionResponse(
                item.id, item.key, item.text, item.type,
                item.options == null ? null : item.options.RootElement.Clone(),
                item.is_required, item.display_order))
            .ToList();
        var userId = CurrentUserId();
        var hasResponded = userId.HasValue && await db.SurveyResponses.AnyAsync(
            item => item.survey_id == survey.id && item.user_id == userId,
            cancellationToken);

        return new SurveyResponseDto(
            survey.id, survey.code, survey.title, survey.description,
            survey.starts_at, survey.ends_at, survey.allow_anonymous,
            survey.show_results, survey.is_published, questions, hasResponded);
    }

    private Dictionary<string, string[]> ValidateAnswers(
        IReadOnlyList<SurveyQuestion> questions,
        Dictionary<string, JsonElement>? answers)
    {
        var errors = new Dictionary<string, string[]>();
        answers ??= new Dictionary<string, JsonElement>();
        var knownKeys = questions.Select(item => item.key).ToHashSet(StringComparer.Ordinal);
        foreach (var unknown in answers.Keys.Where(key => !knownKeys.Contains(key)))
        {
            errors[$"answers.{unknown}"] = ["Întrebarea nu există în chestionar."];
        }

        foreach (var question in questions)
        {
            if (!answers.TryGetValue(question.key, out var answer))
            {
                if (question.is_required)
                {
                    errors[$"answers.{question.key}"] = ["Răspunsul este obligatoriu."];
                }
                continue;
            }

            if (!IsValidAnswer(question, answer))
            {
                errors[$"answers.{question.key}"] = ["Formatul răspunsului nu corespunde întrebării."];
            }
        }
        return errors;
    }

    private static bool IsValidAnswer(SurveyQuestion question, JsonElement answer)
    {
        if (answer.ValueKind == JsonValueKind.Null) return false;
        if (question.type == SurveyQuestionType.FreeText)
            return answer.ValueKind == JsonValueKind.String && (answer.GetString()?.Length ?? 0) <= 5000;
        if (question.type == SurveyQuestionType.Rating)
        {
            if (!answer.TryGetInt32(out var number) || question.options is null) return false;
            var options = question.options.RootElement;
            return options.TryGetProperty("min", out var min) && min.TryGetInt32(out var minValue) &&
                options.TryGetProperty("max", out var max) && max.TryGetInt32(out var maxValue) &&
                number >= minValue && number <= maxValue;
        }

        if (question.options is null || !TryGetChoiceValues(question.options.RootElement, out var values)) return false;
        if (question.type == SurveyQuestionType.SingleChoice)
            return answer.ValueKind == JsonValueKind.String && values.Contains(answer.GetString()!);
        return answer.ValueKind == JsonValueKind.Array && answer.GetArrayLength() > 0 &&
            answer.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String && values.Contains(item.GetString()!));
    }

    private static bool TryGetChoiceValues(JsonElement options, out HashSet<string> values)
    {
        values = new HashSet<string>(StringComparer.Ordinal);
        if (options.ValueKind != JsonValueKind.Array) return false;
        foreach (var item in options.EnumerateArray())
        {
            if (!item.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.String) return false;
            values.Add(value.GetString()!);
        }
        return values.Count > 0;
    }

    private async Task<SurveyResultsResponse> BuildResultsAsync(Survey survey, CancellationToken cancellationToken)
    {
        var questions = await db.SurveyQuestions.AsNoTracking().Where(item => item.survey_id == survey.id).OrderBy(item => item.display_order).ToListAsync(cancellationToken);
        var responses = await db.SurveyResponses.AsNoTracking().Where(item => item.survey_id == survey.id).ToListAsync(cancellationToken);
        var items = questions.Select(question =>
        {
            var counts = new Dictionary<string, int>();
            var responseCount = 0;
            foreach (var response in responses)
            {
                if (!response.answers.RootElement.TryGetProperty(question.key, out var answer)) continue;
                responseCount++;
                foreach (var value in answer.ValueKind == JsonValueKind.Array ? answer.EnumerateArray().ToList() : [answer])
                {
                    var key = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
                    counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
                }
            }
            return new SurveyResultItem(question.key, question.type, responseCount, counts);
        }).ToList();
        return new SurveyResultsResponse(survey.id, survey.code, responses.Count, items, []);
    }

    private Guid? CurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private ProblemDetails Problem(string detail) =>
        ApiProblemDetails.Create(HttpContext, StatusCodes.Status404NotFound, "Chestionar indisponibil", detail);
}
