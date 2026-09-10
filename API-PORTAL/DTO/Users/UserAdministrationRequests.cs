using API_PORTAL.Entities.Base;
using System.ComponentModel.DataAnnotations;

namespace API_PORTAL.DTO.Users
{
    public sealed class UpdateUserActiveRequest
    {
        public bool IsActive { get; set; }
    }

    public sealed class UpdateUserRoleRequest
    {
        [Required]
        public Role? Role { get; set; }
    }
}
