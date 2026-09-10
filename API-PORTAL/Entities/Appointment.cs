using API_PORTAL.Entities.Base;

namespace API_PORTAL.Entities
{
    public enum AppointmentStatus
    {
        Requested,
        Confirmed,
        Rejected,
        Cancelled,
        Completed,
        NoShow
    }
    public class Appointment : BaseEntity
    {
        public Guid id { get; set; }

        public Guid slot_id { get; set; }
        public AppointmentSlot slot { get; set; } = null!;

        public Guid user_id { get; set; }
        public User user { get; set; } = null!;

        public AppointmentStatus status { get; set; } = AppointmentStatus.Requested;

        public string? notes { get; set; }

        public string? decision_note { get; set; }

        public Guid? decided_by_user_id { get; set; }
        public User? decided_by_user { get; set; }

        public DateTimeOffset? decided_at { get; set; }

        public string reference_code { get; set; } = null!;
    }
}
