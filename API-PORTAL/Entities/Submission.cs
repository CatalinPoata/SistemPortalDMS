using API_PORTAL.Entities.Base;
using System.Text.Json;

namespace API_PORTAL.Entities
{

    public enum SubmissionStatus
    {
        Submitted,
        Registered,
        InReview,
        InfoRequested,
        Completed,
        Rejected,
        Cancelled
    }
    public class Submission : BaseEntity
    {
        public Guid id { get; set; }

        public Guid external_id { get; set; }

        public Guid service_id { get; set; }
        public ServiceDefinition service { get; set; } = null!;

        public int schema_version { get; set; }

        public JsonDocument form_snapshot { get; set; } = null!;

        public Guid user_id { get; set; }
        public User user { get; set; } = null!;

        public JsonDocument values { get; set; } = null!;

        public SubmissionStatus status { get; set; }
            = SubmissionStatus.Submitted;

        public string? status_details { get; set; }

        public DateTimeOffset submitted_at { get; set; }

        public long? registry_number { get; set; }

        public int? registry_year { get; set; }

        public string? registry_display_number { get; set; }

        public DateTimeOffset? registered_at { get; set; }

        public Guid? dms_entry_id { get; set; }
    }
}
