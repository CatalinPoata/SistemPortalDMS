namespace API_PORTAL.DTO.Auth
{
    public sealed record TokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt);
}
