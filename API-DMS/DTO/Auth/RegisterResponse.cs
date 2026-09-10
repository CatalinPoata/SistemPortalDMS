namespace API_DMS.DTO.Auth
{
    public sealed record RegisterResponse(
    string Message,
    string? DevelopmentConfirmationToken);
}
