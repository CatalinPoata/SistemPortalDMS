namespace API_PORTAL.DTO.Auth
{
    public sealed record ResendConfirmationResponse(
    string Message,
    string? DevelopmentConfirmationToken);
}
