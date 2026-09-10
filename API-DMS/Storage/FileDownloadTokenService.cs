using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace API_DMS.Storage
{
    public sealed class FileDownloadTokenService
    {
        private readonly byte[] signingKey;
        private readonly int lifetimeMinutes;
        private readonly TimeProvider timeProvider;

        public FileDownloadTokenService(
            IOptions<FileDownloadOptions> options,
            TimeProvider timeProvider)
        {
            signingKey = Encoding.UTF8.GetBytes(
                options.Value.SigningKey);

            lifetimeMinutes =
                options.Value.LifetimeMinutes;

            this.timeProvider = timeProvider;
        }

        public (string Token, DateTimeOffset ExpiresAt) Create(
            Guid documentId)
        {
            var expiresAt = timeProvider
                .GetUtcNow()
                .AddMinutes(lifetimeMinutes);

            var payload =
                $"{documentId:N}.{expiresAt.ToUnixTimeSeconds()}";

            var signature = Sign(payload);

            var token =
                $"{WebEncoders.Base64UrlEncode(
                    Encoding.UTF8.GetBytes(payload))}." +
                $"{WebEncoders.Base64UrlEncode(signature)}";

            return (token, expiresAt);
        }

        public bool TryValidate(
            string token,
            Guid documentId,
            out DateTimeOffset expiresAt)
        {
            expiresAt = default;

            try
            {
                var parts = token.Split('.', 2);

                if (parts.Length != 2)
                {
                    return false;
                }

                var payload = Encoding.UTF8.GetString(
                    WebEncoders.Base64UrlDecode(parts[0]));

                var receivedSignature =
                    WebEncoders.Base64UrlDecode(parts[1]);

                var expectedSignature = Sign(payload);

                if (!CryptographicOperations.FixedTimeEquals(
                        receivedSignature,
                        expectedSignature))
                {
                    return false;
                }

                var payloadParts = payload.Split('.');

                if (payloadParts.Length != 2 ||
                    !Guid.TryParseExact(
                        payloadParts[0],
                        "N",
                        out var tokenDocumentId) ||
                    tokenDocumentId != documentId ||
                    !long.TryParse(
                        payloadParts[1],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var expiryUnixSeconds))
                {
                    return false;
                }

                expiresAt = DateTimeOffset
                    .FromUnixTimeSeconds(expiryUnixSeconds);

                return expiresAt > timeProvider.GetUtcNow();
            }
            catch
            {
                return false;
            }
        }

        private byte[] Sign(string value)
        {
            using var hmac = new HMACSHA256(signingKey);

            return hmac.ComputeHash(
                Encoding.UTF8.GetBytes(value));
        }
    }
}
