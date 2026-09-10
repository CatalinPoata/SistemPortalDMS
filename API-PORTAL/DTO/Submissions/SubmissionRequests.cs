using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace API_PORTAL.DTO.Submissions
{
    public sealed class CreateSubmissionRequest
    {
        [Required]
        [StringLength(50)]
        public string? ServiceCode { get; set; }

        [Required]
        public JsonElement? Values { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalProperties
        {
            get;
            set;
        }
    }

    public sealed class CreateSubmissionMultipartRequest
    {
        [Required]
        [StringLength(50)]
        public string? ServiceCode { get; set; }

        [Required]
        public string? Values { get; set; }

        public List<IFormFile> Files { get; set; } = [];

        public List<string> FileKeys { get; set; } = [];
    }

    public sealed class WithdrawSubmissionRequest
    {
        [Required]
        [StringLength(1000, MinimumLength = 1)]
        public string? Reason { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalProperties
        {
            get;
            set;
        }
    }

    public sealed class UploadClarificationsRequest
    {
        [Required]
        [MinLength(1)]
        public List<IFormFile> Files { get; set; } = [];
    }
}
