using System.ComponentModel.DataAnnotations;

namespace API_DMS.DTO.Auth
{
    public sealed class RevokeRequest
    {
        [Required]
        public string RefreshToken { get; set; } = null!;
    }
}
