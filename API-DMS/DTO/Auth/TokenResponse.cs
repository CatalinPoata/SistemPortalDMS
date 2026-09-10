namespace API_DMS.DTO.Auth
{
    public sealed record TokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt);
}
