using API_DMS.Entities.Base;
using System.Text.Json;

namespace API_DMS.Entities
{
    public enum EntryStatus
    {
        Submitted,
        Registered,
        InReview,
        InfoRequested,
        Completed,
        Rejected,
        Cancelled
    }

    public enum EntryDirection
    {
        In,
        Out
    }

    public class RegistryEntry : BaseEntity
    {
        public Guid id { get; set; }
        public Guid? external_id { get; set; }

        public Guid registry_type_id { get; set; }
        public RegistryType registry_type { get; set; } = null!;

        public int year { get; set; }
        public long number { get; set; }
        public EntryDirection direction { get; set; }
        public DateTimeOffset registered_at { get; set; }
        public DateTimeOffset? submitted_at { get; set; }

        public string subject { get; set; } = null!;
        public string applicant_name { get; set; } = null!;
        public string? applicant_national_id { get; set; }
        public string? applicant_email { get; set; }
        public string? applicant_phone { get; set; }
        public string? applicant_address { get; set; }

        public string? source_doc_number { get; set; }
        public DateOnly? source_doc_date { get; set; }

        public Guid? department_id { get; set; }
        public Department? department { get; set; }

        public string? service_code { get; set; }
        public JsonDocument? form_values { get; set; }
        public DateOnly deadline { get; set; }

        public EntryStatus status { get; set; } = EntryStatus.Registered;
        public string? status_note { get; set; }
        public Guid created_by_user_id { get; set; }
        public User created_by_user { get; set; } = null!;
    }
}
