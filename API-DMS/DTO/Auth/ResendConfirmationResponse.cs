namespace API_DMS.DTO.Auth
{
    public sealed record ResendConfirmationResponse(
    string Message,
    string? DevelopmentConfirmationToken);
}
