namespace API_PORTAL.DTO.Auth
{
    public sealed record ForgotPasswordResponse(
    string Message,
    string? DevelopmentResetToken);
}
