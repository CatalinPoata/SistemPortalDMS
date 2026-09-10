using System.ComponentModel.DataAnnotations;

namespace API_PORTAL.DTO.Appointments
{
    public sealed class GenerateAppointmentSlotsRequest
    {
        [Required]
        public DateOnly? StartDate { get; set; }

        [Required]
        public DateOnly? EndDate { get; set; }

        [Required]
        public TimeOnly? DayStartsAt { get; set; }

        [Required]
        public TimeOnly? DayEndsAt { get; set; }

        [Required]
        [MinLength(1)]
        public int[]? Weekdays { get; set; }

        [Range(1, int.MaxValue)]
        public int Capacity { get; set; } = 1;

        [Required]
        [StringLength(100)]
        public string? TimeZoneId { get; set; }
    }

    public sealed class SetAppointmentSlotBlockRequest
    {
        public bool IsBlocked { get; set; }
    }
}
