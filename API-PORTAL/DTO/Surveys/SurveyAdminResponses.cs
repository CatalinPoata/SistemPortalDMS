namespace API_PORTAL.DTO.Surveys;

public sealed record SurveySubmissionResponse(
    Guid Id,
    string Code,
    bool ShowResults,
    DateTimeOffset SubmittedAt,
    SurveyResultsResponse? Results);
