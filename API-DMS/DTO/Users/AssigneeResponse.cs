using API_DMS.Entities.Base;

namespace API_DMS.DTO.Users
{
    public sealed record AssigneeResponse(
        Guid Id,
        string FullName,
        string Email,
        Role Role);
}
