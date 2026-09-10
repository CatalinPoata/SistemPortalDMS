using API_DMS.Entities;
using System.Text.Json;

namespace API_DMS.DTO.RegistryEntries
{
    public sealed record RegistryEntryDetailsResponse(
    Guid Id,
    Guid? ExternalId,

    Guid RegistryTypeId,
    string RegistryTypeCode,
    string RegistryTypeName,

    int Year,
    long Number,
    string DisplayNumber,
    EntryDirection Direction,

    DateTimeOffset RegisteredAt,
    DateTimeOffset? SubmittedAt,

    string Subject,
    string ApplicantName,
    string? ApplicantNationalId,
    string? ApplicantEmail,
    string? ApplicantPhone,
    string? ApplicantAddress,

    string? SourceDocNumber,
    DateOnly? SourceDocDate,

    Guid? DepartmentId,
    string? DepartmentCode,
    string? DepartmentName,

    string? ServiceCode,
    JsonDocument? FormValues,

    DateOnly Deadline,
    EntryStatus Status,
    string? StatusNote,

    Guid CreatedByUserId,

    IReadOnlyList<RegistryDocumentDetailsResponse> Documents,
    IReadOnlyList<RegistryTaskDetailsResponse> Tasks,
    IReadOnlyList<EntryEventDetailsResponse> Events);
}
