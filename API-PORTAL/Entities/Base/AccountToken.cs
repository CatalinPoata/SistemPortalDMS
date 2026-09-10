namespace API_PORTAL.Entities.Base
{
    public sealed class AccountToken : BaseEntity
    {
        public Guid id { get; set; }

        public Guid user_id { get; set; }

        public User user { get; set; } = null!;

        public string token_hash { get; set; } = null!;

        public AccountTokenPurpose purpose { get; set; }

        public DateTimeOffset expires_at { get; set; }

        public DateTimeOffset? consumed_at { get; set; }
    }
}
