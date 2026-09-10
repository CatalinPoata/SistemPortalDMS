using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace API_PORTAL.DTO.ServiceDefinitions
{
    public sealed class CreateServiceDefinitionRequest
    {
        [Required]
        [StringLength(50)]
        public string? Code { get; set; }

        [Required]
        [StringLength(200)]
        public string? Title { get; set; }

        [StringLength(500)]
        public string? ShortDescription { get; set; }

        public string? Description { get; set; }

        [Required]
        [StringLength(30)]
        public string? RegistryTypeCode { get; set; }

        [Required]
        public JsonElement? FormSchema { get; set; }

        public bool RequiresAttachment { get; set; }

        [Range(0, int.MaxValue)]
        public int MaxAttachments { get; set; } = 3;

        public int DisplayOrder { get; set; }
    }

    public sealed class UpdateServiceDefinitionRequest
    {
        [Required]
        [StringLength(200)]
        public string? Title { get; set; }

        [StringLength(500)]
        public string? ShortDescription { get; set; }

        public string? Description { get; set; }

        [Required]
        [StringLength(30)]
        public string? RegistryTypeCode { get; set; }

        [Required]
        public JsonElement? FormSchema { get; set; }

        public bool RequiresAttachment { get; set; }

        [Range(0, int.MaxValue)]
        public int MaxAttachments { get; set; } = 3;

        public int DisplayOrder { get; set; }

        [Range(1, int.MaxValue)]
        public int ExpectedSchemaVersion { get; set; }
    }
}
