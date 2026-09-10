using System.ComponentModel.DataAnnotations;

namespace API_DMS.DTO.Auth
{
    public sealed class RefreshRequest
    {
        [Required]
        public string RefreshToken { get; set; } = null!;
    }
}
