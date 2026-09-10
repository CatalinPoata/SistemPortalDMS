using API_PORTAL.Entities.Base;

namespace API_PORTAL.Entities
{
    public class Notification : BaseEntity
    {
        public Guid id { get; set; }

        public Guid user_id { get; set; }
        public User user { get; set; } = null!;

        public string subject { get; set; } = null!;
        public string body { get; set; } = null!;
        public string link_url { get; set; } = null!;

        public DateTimeOffset? read_at { get; set; }
        public DateTimeOffset? sent_at { get; set; }
    }
}
