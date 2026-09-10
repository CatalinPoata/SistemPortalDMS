using API_DMS.Entities;

namespace API_DMS.DTO.Integration
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
