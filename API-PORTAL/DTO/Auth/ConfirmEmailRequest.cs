using System.ComponentModel.DataAnnotations;

namespace API_PORTAL.DTO.Auth
{
    public sealed class ConfirmEmailRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;
    }
}
