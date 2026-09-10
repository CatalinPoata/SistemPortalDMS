using API_DMS.Entities.Base;

namespace API_DMS.Entities
{
    public class InboxEvent : BaseEntity
    {
        public Guid event_id { get; set; }

        public string source { get; set; } = null!;

        public string event_type { get; set; } = null!;

        public string payload { get; set; } = null!;

        public DateTimeOffset processed_at { get; set; }
    }
}
