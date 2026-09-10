using API_PORTAL.Entities.Base;

namespace API_PORTAL.DTO.Users
{
    public sealed record PortalUserAdministrationResponse(
        Guid Id,
        string Email,
        string FullName,
        Role Role,
        bool EmailConfirmed,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt);
}
