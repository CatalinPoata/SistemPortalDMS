namespace API_PORTAL.DTO.Auth
{
    public sealed record RegisterResponse(
    string Message,
    string? DevelopmentConfirmationToken);
}
