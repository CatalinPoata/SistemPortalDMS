using System.Text.Json;
using API_PORTAL.Entities;

namespace API_PORTAL.DTO.Surveys;

public sealed record SurveyQuestionResponse(
    Guid Id,
    string Key,
    string Text,
    SurveyQuestionType Type,
    JsonElement? Options,
    bool IsRequired,
    int DisplayOrder);

public sealed record SurveyResponseSummary(
    Guid Id,
    DateTimeOffset SubmittedAt,
    JsonElement Answers);

public sealed record SurveyResultItem(
    string Key,
    SurveyQuestionType Type,
    int ResponseCount,
    IReadOnlyDictionary<string, int> ValueCounts);

public sealed record SurveyResultsResponse(
    Guid SurveyId,
    string Code,
    int TotalResponses,
    IReadOnlyList<SurveyResultItem> Items,
    IReadOnlyList<SurveyResponseSummary> Responses);

public sealed record SurveyResponse(
    Guid Id,
    string Code,
    string Title,
    string? Description,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    bool AllowAnonymous,
    bool ShowResults,
    bool IsPublished,
    IReadOnlyList<SurveyQuestionResponse> Questions,
    bool HasResponded);

public sealed record SurveyListItemResponse(
    Guid Id,
    string Code,
    string Title,
    string? Description,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    bool AllowAnonymous,
    bool ShowResults,
    bool IsPublished,
    int QuestionCount,
    int ResponseCount);

public sealed record PagedSurveyResponse(
    IReadOnlyList<SurveyListItemResponse> Items,
    int Page,
    int PageSize,
    int Total);
