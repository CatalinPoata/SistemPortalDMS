using API_PORTAL.Entities.Base;

namespace API_PORTAL.Entities
{
    public class Article : BaseEntity
    {
        public Guid id { get; set; }

        public string slug { get; set; } = null!;
        public string title { get; set; } = null!;
        public string? summary { get; set; }
        public string body { get; set; } = null!;

        public DateTimeOffset? published_at { get; set; }

        public bool is_published { get; set; } = false;
    }
}
