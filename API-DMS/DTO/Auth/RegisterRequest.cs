using API_DMS.Validation;
using System.ComponentModel.DataAnnotations;

namespace API_DMS.DTO.Auth
{
    public sealed class RegisterRequest
    {
        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StrongPassword]
        public string Password { get; set; } = string.Empty;

        [Required]
        [StringLength(200, MinimumLength = 2)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(13)]
        public string? NationalId { get; set; }

        [StringLength(30)]
        public string? Phone { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }
    }
}
