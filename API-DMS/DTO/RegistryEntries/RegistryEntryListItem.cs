using API_DMS.Entities;

namespace API_DMS.DTO.RegistryEntries
{
    public sealed record RegistryEntryListItem(
    Guid Id,
    Guid RegistryTypeId,
    string RegistryTypeCode,
    string RegistryTypeName,
    int Year,
    long Number,
    string DisplayNumber,
    EntryDirection Direction,
    DateTimeOffset RegisteredAt,
    DateOnly Deadline,
    string Subject,
    string ApplicantName,
    EntryStatus Status);
}
