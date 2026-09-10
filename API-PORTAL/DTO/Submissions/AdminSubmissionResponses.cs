using API_PORTAL.Entities;
using System.Text.Json;

namespace API_PORTAL.DTO.Submissions;

public sealed record AdminSubmissionListItemResponse(
    Guid Id,
    Guid ExternalId,
    string ServiceCode,
    string ServiceTitle,
    SubmissionStatus Status,
    string ApplicantName,
    string ApplicantEmail,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? RegisteredAt,
    string? RegistryDisplayNumber);

public sealed record PagedAdminSubmissionResponse(
    IReadOnlyList<AdminSubmissionListItemResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record AdminSubmissionDetailsResponse(
    Guid Id,
    Guid ExternalId,
    string ServiceCode,
    string ServiceTitle,
    int SchemaVersion,
    string ApplicantName,
    string ApplicantEmail,
    SubmissionStatus Status,
    string? StatusDetails,
    DateTimeOffset SubmittedAt,
    long? RegistryNumber,
    int? RegistryYear,
    string? RegistryDisplayNumber,
    DateTimeOffset? RegisteredAt,
    Guid? DmsEntryId,
    JsonElement FormSnapshot,
    JsonElement Values,
    IReadOnlyList<SubmissionFileResponse> Files,
    IReadOnlyList<SubmissionEventResponse> Events);
