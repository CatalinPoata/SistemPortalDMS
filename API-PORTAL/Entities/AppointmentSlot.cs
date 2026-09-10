using API_PORTAL.Entities.Base;

namespace API_PORTAL.Entities
{
    public class AppointmentSlot : BaseEntity
    {
        public Guid id { get; set; }

        public Guid appointment_type_id { get; set; }
        public AppointmentType appointment_type { get; set; } = null!;

        public DateTimeOffset starts_at { get; set; }
        public DateTimeOffset ends_at { get; set; }

        public int capacity { get; set; } = 1;

        public int booked_count { get; set; } = 0;

        public bool is_blocked { get; set; } = false;
    }
}
