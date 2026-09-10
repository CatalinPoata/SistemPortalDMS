using System.ComponentModel.DataAnnotations;

namespace API_DMS.DTO.Auth
{
    public sealed class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
