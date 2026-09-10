using System.ComponentModel.DataAnnotations;

namespace API_PORTAL.DTO.Appointments
{
    public sealed class CreateAppointmentRequest
    {
        [Required]
        public Guid SlotId { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    public sealed class AppointmentDecisionRequest
    {
        [StringLength(1000)]
        public string? DecisionNote { get; set; }
    }
}
