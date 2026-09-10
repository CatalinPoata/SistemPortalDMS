using API_PORTAL.Entities.Base;

namespace API_PORTAL.Entities
{
    public class User : BaseEntity
    {
        public Guid id { get; set; }
        public string email { get; set; } = null!;
        public string password_hash { get; set; } = null!;
        public Role role { get; set; } = Role.Citizen;

        public string full_name { get; set; } = null!;

        public string? national_id { get; set; }
        public string? phone { get; set; }
        public string? address { get; set; }
        public bool email_confirmed { get; set; } = false;
        public bool is_active { get; set; } = true;
        public int failed_login_count { get; set; }
        public DateTimeOffset? lockout_end { get; set; }

    }
}
