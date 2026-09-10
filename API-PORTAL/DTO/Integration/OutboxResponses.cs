using API_PORTAL.Entities;

namespace API_PORTAL.DTO.Integration
{
    public sealed record OutboxMessageResponse(
        Guid Id,
        string AggregateType,
        Guid AggregateId,
        string EventType,
        OutboxMessageStatus Status,
        int Attempts,
        DateTimeOffset NextAttemptAt,
        string? LastError,
        DateTimeOffset? DeliveredAt,
        DateTime CreatedAt);
}
