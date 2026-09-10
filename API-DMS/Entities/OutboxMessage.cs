using API_DMS.Entities.Base;
using System.Text.Json;

namespace API_DMS.Entities
{
    public enum OutboxMessageStatus
    {
        Pending,
        Delivered,
        Failed
    }

    public class OutboxMessage : BaseEntity
    {
        public Guid id { get; set; }

        public string aggregate_type { get; set; } = null!;
        public Guid aggregate_id { get; set; }

        public string event_type { get; set; } = null!;

        public JsonDocument payload { get; set; } = null!;

        public OutboxMessageStatus status { get; set; }
            = OutboxMessageStatus.Pending;

        public int attempts { get; set; }

        public DateTimeOffset next_attempt_at { get; set; }

        public string? last_error { get; set; }

        public DateTimeOffset? delivered_at { get; set; }
    }
}
