using System.ComponentModel.DataAnnotations;

namespace API_DMS.DTO.Auth
{
    public sealed class ResendConfirmationRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
