using System.ComponentModel.DataAnnotations;

namespace API_PORTAL.DTO.Appointments
{
    public sealed class CreateAppointmentTypeRequest
    {
        [Required]
        [StringLength(40)]
        public string? Code { get; set; }

        [Required]
        [StringLength(200)]
        public string? Name { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(250)]
        public string? Location { get; set; }

        [Range(1, int.MaxValue)]
        public int DurationMinutes { get; set; }

        public bool RequiresConfirmation { get; set; } = true;

        [Range(0, int.MaxValue)]
        public int MaxDaysAhead { get; set; } = 30;

        public bool IsActive { get; set; } = true;
    }

    public sealed class UpdateAppointmentTypeRequest
    {
        [Required]
        [StringLength(200)]
        public string? Name { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(250)]
        public string? Location { get; set; }

        [Range(1, int.MaxValue)]
        public int DurationMinutes { get; set; }

        public bool RequiresConfirmation { get; set; } = true;

        [Range(0, int.MaxValue)]
        public int MaxDaysAhead { get; set; } = 30;

        public bool IsActive { get; set; } = true;
    }
}
