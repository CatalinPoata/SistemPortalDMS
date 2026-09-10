using System.ComponentModel.DataAnnotations;

namespace API_DMS.DTO.Auth
{
    public sealed class ConfirmEmailRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;
    }
}
