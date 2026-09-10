using API_DMS.Entities;

namespace API_DMS.DTO.RegistryEntries
{
    public sealed record RegistryEntryResponse(
    Guid Id,
    Guid RegistryTypeId,
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
