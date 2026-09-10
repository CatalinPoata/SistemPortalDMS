using API_PORTAL.Entities.Base;

namespace API_PORTAL.Entities
{
    public class AppointmentType : BaseEntity
    {
        public Guid id { get; set; }

        public string code { get; set; } = null!;
        public string name { get; set; } = null!;
        public string? description { get; set; }
        public string? location { get; set; }

        public int duration_minutes { get; set; }

        public bool requires_confirmation { get; set; } = true;

        public int max_days_ahead { get; set; } = 30;

        public bool is_active { get; set; } = true;
    }
}
