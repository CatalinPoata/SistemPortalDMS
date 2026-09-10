using API_PORTAL.Entities.Base;

namespace API_PORTAL.Entities
{
    public class PublicRegistryDocument : BaseEntity
    {
        public Guid id { get; set; }

        public Guid entry_id { get; set; }
        public PublicRegistryEntry entry { get; set; } = null!;

        public string storage_key { get; set; } = null!;
        public string original_name { get; set; } = null!;
        public string content_type { get; set; } = null!;
        public long size_bytes { get; set; }
        public string sha256 { get; set; } = null!;

        public int display_order { get; set; } = 0;
    }
}
