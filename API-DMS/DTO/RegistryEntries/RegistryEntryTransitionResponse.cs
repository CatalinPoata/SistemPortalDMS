using API_DMS.Entities;

namespace API_DMS.DTO.RegistryEntries
{
    public sealed record RegistryEntryTransitionResponse(
    Guid EntryId,
    EntryStatus PreviousStatus,
    EntryStatus Status,
    string? StatusNote,
    DateTimeOffset OccurredAt);
}
