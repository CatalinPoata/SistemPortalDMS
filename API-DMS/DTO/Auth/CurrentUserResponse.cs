namespace API_DMS.DTO.Auth
{
    public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string FullName,
    string Role);
}
