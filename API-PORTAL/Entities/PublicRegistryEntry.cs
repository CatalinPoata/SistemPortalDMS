using API_PORTAL.Entities.Base;

namespace API_PORTAL.Entities
{
    public class PublicRegistryEntry : BaseEntity
    {
        public Guid id { get; set; }

        public Guid registry_id { get; set; }
        public PublicRegistry registry { get; set; } = null!;

        public string position_number { get; set; } = null!;
        public string title { get; set; } = null!;
        public DateOnly entry_date { get; set; }
        public string? description { get; set; }

        public bool is_published { get; set; } = false;
    }
}
