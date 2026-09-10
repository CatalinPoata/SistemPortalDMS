using API_DMS.Entities.Base;
using System.Text.Json;

namespace API_DMS.Entities
{
    public enum EventType
    {
        Created,
        StatusChanged,
        Assigned,
        DocumentAdded,
        TaskCompleted
    }

    public class EntryEvent : BaseEntity
    {
        public Guid id { get; set; }

        public Guid entry_id { get; set; }
        public RegistryEntry entry { get; set; } = null!;

        public DateTimeOffset occurred_at { get; set; }

        public Guid? actor_user_id { get; set; }
        public User? actor_user { get; set; } = null!;

        public EventType type { get; set; }
        public string message { get; set; } = null!;
        public JsonDocument payload { get; set; } = null!;
    }
}
