namespace API_DMS.DTO.Auth
{
    public sealed record ForgotPasswordResponse(
    string Message,
    string? DevelopmentResetToken);
}
