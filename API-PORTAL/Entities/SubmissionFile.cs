using API_PORTAL.Entities.Base;

namespace API_PORTAL.Entities
{
    public enum SubmissionFileKind
    {
        Application,
        Attachment,
        Response
    }

    public class SubmissionFile : BaseEntity
    {
        public Guid id { get; set; }

        public Guid submission_id { get; set; }
        public Submission submission { get; set; } = null!;

        public string? field_key { get; set; }

        public SubmissionFileKind kind { get; set; }

        public string storage_key { get; set; } = null!;
        public string original_name { get; set; } = null!;
        public string content_type { get; set; } = null!;

        public long size_bytes { get; set; }

        public string sha256 { get; set; } = null!;
    }
}
