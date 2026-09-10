using API_PORTAL.Entities.Base;

namespace API_PORTAL.Entities
{
    public class PublicRegistry : BaseEntity
    {
        public Guid id { get; set; }

        public string code { get; set; } = null!;
        public string name { get; set; } = null!;
        public string? description { get; set; }

        public bool is_published { get; set; } = false;
        public int display_order { get; set; } = 0;
    }
}
