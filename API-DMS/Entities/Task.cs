using API_DMS.Entities.Base;

namespace API_DMS.Entities
{
    public enum TaskStatus
    {
        Open,
        Done,
        Cancelled
    }

    public class Task : BaseEntity
    {
        public Guid id { get; set; }

        public Guid entry_id { get; set; }
        public RegistryEntry entry { get; set; } = null!;

        public Guid? assignee_user_id { get; set; }
        public User? assignee_user { get; set; }

        public Guid? department_id { get; set; }
        public Department? department { get; set; }

        public Guid created_by_user_id { get; set; }
        public User created_by_user { get; set; } = null!;

        public string title { get; set; } = null!;
        public string? instructions { get; set; }
        public DateOnly? due_date { get; set; }

        public TaskStatus status { get; set; } = TaskStatus.Open;
        public string? resolution_note { get; set; }
        public DateTimeOffset? completed_at { get; set; }
    }
}
