using API_DMS.Entities;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace API_DMS.Auth
{
    public sealed record IssuedTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    RefreshToken RefreshTokenEntity);

    public interface IJwtTokenService
    {
        IssuedTokens Create(User user);

        string HashRefreshToken(string refreshToken);
    }

    public sealed class JwtTokenService : IJwtTokenService
    {
        private readonly JwtOptions options;

        public JwtTokenService(IOptions<JwtOptions> options)
        {
            this.options = options.Value;
        }

        public IssuedTokens Create(User user)
        {
            var now = DateTimeOffset.UtcNow;
            var accessTokenExpiresAt =
                now.AddMinutes(options.AccessTokenMinutes);

            var refreshTokenExpiresAt =
                now.AddDays(options.RefreshTokenDays);

            var claims = new[]
            {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                user.id.ToString()),

            new Claim(
                JwtRegisteredClaimNames.Email,
                user.email),

            new Claim(
                ClaimTypes.Role,
                user.role.ToString()),

            new Claim(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString())
        };

            var signingKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(options.SigningKey));

            var credentials = new SigningCredentials(
                signingKey,
                SecurityAlgorithms.HmacSha256);

            var jwt = new JwtSecurityToken(
                issuer: options.Issuer,
                audience: options.Audience,
                claims: claims,
                notBefore: now.UtcDateTime,
                expires: accessTokenExpiresAt.UtcDateTime,
                signingCredentials: credentials);

            var accessToken =
                new JwtSecurityTokenHandler().WriteToken(jwt);

            var refreshToken =
                Base64UrlEncoder.Encode(
                    RandomNumberGenerator.GetBytes(64));

            var refreshTokenEntity = new RefreshToken
            {
                id = Guid.NewGuid(),
                user_id = user.id,
                token_hash = HashRefreshToken(refreshToken),
                expires_at = refreshTokenExpiresAt
            };

            return new IssuedTokens(
                accessToken,
                accessTokenExpiresAt,
                refreshToken,
                refreshTokenEntity);
        }

        public string HashRefreshToken(string refreshToken)
        {
            var hash = SHA256.HashData(
                Encoding.UTF8.GetBytes(refreshToken));

            return Convert.ToHexString(hash)
                .ToLowerInvariant();
        }
    }
}
