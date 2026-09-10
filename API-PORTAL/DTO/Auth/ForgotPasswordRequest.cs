using System.ComponentModel.DataAnnotations;

namespace API_PORTAL.DTO.Auth
{
    public sealed class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
