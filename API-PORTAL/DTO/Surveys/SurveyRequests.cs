using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using API_PORTAL.Entities;

namespace API_PORTAL.DTO.Surveys;

public class CreateSurveyRequest
{
    [Required, StringLength(50)]
    public string? Code { get; set; }

    [Required, StringLength(250)]
    public string? Title { get; set; }

    public string? Description { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public bool AllowAnonymous { get; set; }
    public bool ShowResults { get; set; }
}

public sealed class UpdateSurveyRequest : CreateSurveyRequest
{
    public bool IsPublished { get; set; }
}

public sealed class UpsertSurveyQuestionRequest
{
    [Required, StringLength(60)]
    public string? Key { get; set; }

    [Required, StringLength(1000)]
    public string? Text { get; set; }

    public SurveyQuestionType Type { get; set; }
    public JsonElement? Options { get; set; }
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class SubmitSurveyResponseRequest
{
    [Required]
    public Dictionary<string, JsonElement>? Answers { get; set; }
}
