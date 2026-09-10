using System.ComponentModel.DataAnnotations;

namespace API_PORTAL.DTO.Auth
{
    public sealed class ResendConfirmationRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
