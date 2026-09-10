using API_PORTAL.Entities.Base;

namespace API_PORTAL.Entities
{
    public class Survey : BaseEntity
    {
        public Guid id { get; set; }

        public string code { get; set; } = null!;
        public string title { get; set; } = null!;
        public string? description { get; set; }

        public DateTimeOffset? starts_at { get; set; }
        public DateTimeOffset? ends_at { get; set; }

        public bool allow_anonymous { get; set; } = false;
        public bool show_results { get; set; } = false;
        public bool is_published { get; set; } = false;
    }
}
