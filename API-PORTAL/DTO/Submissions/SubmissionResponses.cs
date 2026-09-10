using API_PORTAL.Entities;
using System.Text.Json;

namespace API_PORTAL.DTO.Submissions
{
    public sealed record SubmissionListItemResponse(
        Guid Id,
        Guid ExternalId,
        string ServiceCode,
        string ServiceTitle,
        SubmissionStatus Status,
        DateTimeOffset SubmittedAt,
        string? RegistryDisplayNumber);

    public sealed record SubmissionEventResponse(
        Guid Id,
        DateTimeOffset OccurredAt,
        SubmissionEventType Type,
        string Message,
        Guid? FileId);

    public sealed record SubmissionFileResponse(
        Guid Id,
        string? FieldKey,
        SubmissionFileKind Kind,
        string OriginalName,
        string ContentType,
        long SizeBytes,
        string Sha256);

    public sealed record SubmissionFileDownloadUrlResponse(
        Guid FileId,
        string Url,
        DateTimeOffset ExpiresAt);

    public sealed record SubmissionDetailsResponse(
        Guid Id,
        Guid ExternalId,
        string ServiceCode,
        string ServiceTitle,
        int SchemaVersion,
        JsonElement FormSnapshot,
        JsonElement Values,
        SubmissionStatus Status,
        string? StatusDetails,
        DateTimeOffset SubmittedAt,
        long? RegistryNumber,
        int? RegistryYear,
        string? RegistryDisplayNumber,
        DateTimeOffset? RegisteredAt,
        IReadOnlyList<SubmissionFileResponse> Files,
        IReadOnlyList<SubmissionEventResponse> Events);
}
