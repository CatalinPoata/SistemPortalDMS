using API_DMS.Entities.Base;

namespace API_DMS.Entities
{
    public class RefreshToken : BaseEntity
    {
        public Guid id { get; set; }
        public Guid user_id { get; set; }
        public User user { get; set; } = null!;

        public string token_hash { get; set; } = null!;
        public DateTimeOffset expires_at { get; set; }
        public DateTimeOffset? revoked_at { get; set; }

        public Guid? replaced_by_id { get; set; }
        public RefreshToken? replaced_by { get; set; }
    }
}
