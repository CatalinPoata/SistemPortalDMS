using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace API_PORTAL.Storage
{
    public sealed class SubmissionFileDownloadTokenService
    {
        private const string Purpose = "portal-submission-file-v1";

        private readonly byte[] signingKey;
        private readonly int lifetimeMinutes;
        private readonly TimeProvider timeProvider;

        public SubmissionFileDownloadTokenService(
            IOptions<SubmissionFileDownloadOptions> options,
            TimeProvider timeProvider)
        {
            signingKey = Encoding.UTF8.GetBytes(
                options.Value.SigningKey);
            lifetimeMinutes = options.Value.LifetimeMinutes;
            this.timeProvider = timeProvider;
        }

        public (string Token, DateTimeOffset ExpiresAt) Create(Guid fileId)
        {
            var expiresAt = timeProvider
                .GetUtcNow()
                .AddMinutes(lifetimeMinutes);
            var payload = string.Join(
                '.',
                Purpose,
                fileId.ToString("N"),
                expiresAt.ToUnixTimeSeconds().ToString(
                    CultureInfo.InvariantCulture));
            var token =
                $"{WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payload))}." +
                WebEncoders.Base64UrlEncode(Sign(payload));

            return (token, expiresAt);
        }

        public bool TryValidate(
            string token,
            Guid fileId,
            out DateTimeOffset expiresAt)
        {
            expiresAt = default;

            try
            {
                var tokenParts = token.Split('.', 2);

                if (tokenParts.Length != 2)
                {
                    return false;
                }

                var payload = Encoding.UTF8.GetString(
                    WebEncoders.Base64UrlDecode(tokenParts[0]));
                var receivedSignature = WebEncoders.Base64UrlDecode(
                    tokenParts[1]);

                if (!CryptographicOperations.FixedTimeEquals(
                        receivedSignature,
                        Sign(payload)))
                {
                    return false;
                }

                var payloadParts = payload.Split('.');

                if (payloadParts.Length != 3 ||
                    payloadParts[0] != Purpose ||
                    !Guid.TryParseExact(
                        payloadParts[1],
                        "N",
                        out var tokenFileId) ||
                    tokenFileId != fileId ||
                    !long.TryParse(
                        payloadParts[2],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var expiryUnixSeconds))
                {
                    return false;
                }

                expiresAt = DateTimeOffset.FromUnixTimeSeconds(
                    expiryUnixSeconds);

                return expiresAt > timeProvider.GetUtcNow();
            }
            catch (FormatException)
            {
                return false;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        private byte[] Sign(string value)
        {
            using var hmac = new HMACSHA256(signingKey);

            return hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
        }
    }
}
