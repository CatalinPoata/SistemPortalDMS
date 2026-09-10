using API_DMS.Entities;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace API_DMS.DTO.RegistryEntries
{
    public sealed class CreateRegistryEntryRequest
    {
        [Required]
        public Guid RegistryTypeId { get; set; }

        public EntryDirection Direction { get; set; }

        public DateTimeOffset? SubmittedAt { get; set; }

        [Required]
        [StringLength(1000)]
        public string Subject { get; set; } = null!;

        [Required]
        [StringLength(200)]
        public string ApplicantName { get; set; } = null!;

        [StringLength(13)]
        public string? ApplicantNationalId { get; set; }

        [EmailAddress]
        [StringLength(256)]
        public string? ApplicantEmail { get; set; }

        [StringLength(30)]
        public string? ApplicantPhone { get; set; }

        [StringLength(500)]
        public string? ApplicantAddress { get; set; }

        [StringLength(60)]
        public string? SourceDocNumber { get; set; }

        public DateOnly? SourceDocDate { get; set; }
        public DateOnly? Deadline { get; set; }

        public Guid? DepartmentId { get; set; }

        [StringLength(50)]
        public string? ServiceCode { get; set; }

        public JsonElement? FormValues { get; set; }
    }
}
