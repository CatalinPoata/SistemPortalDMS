using API_DMS.Entities;
using System.Text.Json;

namespace API_DMS.DTO.RegistryEntries
{
    public sealed record EntryEventDetailsResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid? ActorUserId,
    EventType Type,
    string Message,
    JsonDocument Payload);
}
