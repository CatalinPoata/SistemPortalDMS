using API_PORTAL.Entities.Base;

namespace API_PORTAL.DTO.Auth
{
    public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string FullName,
    Role Role,
    bool EmailConfirmed);
}
