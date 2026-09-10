using API_PORTAL.Validation;
using System.ComponentModel.DataAnnotations;

namespace API_PORTAL.DTO.Auth
{
    public sealed class ResetPasswordRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [StrongPassword]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare(nameof(NewPassword))]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
