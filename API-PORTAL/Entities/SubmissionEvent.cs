using API_PORTAL.Entities.Base;

namespace API_PORTAL.Entities
{
    public enum SubmissionEventType
    {
        Submitted,
        Registered,
        StatusChanged,
        InfoRequested,
        FileAdded,
        Completed,
        Rejected,
        Cancelled
    }
    public class SubmissionEvent : BaseEntity
    {
        public Guid id { get; set; }

        public Guid submission_id { get; set; }
        public Submission submission { get; set; } = null!;

        public DateTimeOffset occurred_at { get; set; }

        public SubmissionEventType type { get; set; }

        public string message { get; set; } = null!;

        public Guid? file_id { get; set; }
        public SubmissionFile? file { get; set; }
    }
}
