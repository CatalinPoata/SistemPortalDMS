using API_DMS.Entities.Base;

namespace API_DMS.Entities
{
    public enum DocumentDirection
    {
        In,
        Out
    }

    public class RegistryDocument : BaseEntity
    {
        public Guid id { get; set; }
        public Guid entry_id { get; set; }
        public RegistryEntry entry { get; set; } = null!;

        public Guid? external_file_id { get; set; }
        public DocumentDirection direction { get; set; }

        public Guid document_kind_id { get; set; }
        public DocumentKind document_kind { get; set; } = null!;

        public DateOnly? document_date { get; set; }
        public string? issuer { get; set; }
        public string? note { get; set; }

        public string storage_key { get; set; } = null!;
        public string original_name { get; set; } = null!;
        public string content_type { get; set; } = null!;
        public long size_bytes { get; set; }
        public string sha256 { get; set; } = null!;

        public Guid uploaded_by_user_id { get; set; }
        public User uploaded_by_user { get; set; } = null!;
    }
}
