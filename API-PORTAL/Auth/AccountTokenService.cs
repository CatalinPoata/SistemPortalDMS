using API_PORTAL.Entities.Base;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace API_PORTAL.Auth
{
    public sealed record GeneratedAccountToken(
    string RawToken,
    AccountToken Entity);

    public interface IAccountTokenService
    {
        GeneratedAccountToken Create(
            Guid userId,
            AccountTokenPurpose purpose,
            TimeSpan lifetime);

        string Hash(string rawToken);
    }

    public sealed class AccountTokenService
        : IAccountTokenService
    {
        public GeneratedAccountToken Create(
            Guid userId,
            AccountTokenPurpose purpose,
            TimeSpan lifetime)
        {
            var rawToken =
                Base64UrlEncoder.Encode(
                    RandomNumberGenerator.GetBytes(64));

            var entity = new AccountToken
            {
                id = Guid.NewGuid(),
                user_id = userId,
                purpose = purpose,
                token_hash = Hash(rawToken),
                expires_at =
                    DateTimeOffset.UtcNow.Add(lifetime)
            };

            return new GeneratedAccountToken(
                rawToken,
                entity);
        }

        public string Hash(string rawToken)
        {
            var hash = SHA256.HashData(
                Encoding.UTF8.GetBytes(rawToken));

            return Convert.ToHexString(hash)
                .ToLowerInvariant();
        }
    }
}
