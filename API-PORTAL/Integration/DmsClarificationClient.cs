using API_PORTAL.Entities;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace API_PORTAL.Integration
{
    public interface IDmsClarificationClient
    {
        Task<DmsRegistrationDeliveryResult> DeliverAsync(
            OutboxMessage message,
            CancellationToken cancellationToken);
    }

    public sealed class DmsClarificationClient : IDmsClarificationClient
    {
        private readonly HttpClient httpClient;
        private readonly DmsIntegrationOptions options;

        public DmsClarificationClient(
            HttpClient httpClient,
            IOptions<DmsIntegrationOptions> options)
        {
            this.httpClient = httpClient;
            this.options = options.Value;
        }

        public async Task<DmsRegistrationDeliveryResult> DeliverAsync(
            OutboxMessage message,
            CancellationToken cancellationToken)
        {
            var body = message.payload.RootElement.GetRawText();
            var payload = message.payload.RootElement;
            var entryId = payload.GetProperty("entryId").GetGuid();
            var timestamp = DateTimeOffset.UtcNow.ToString(
                "O",
                CultureInfo.InvariantCulture);

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"api/integration/registry-entries/{entryId}/documents")
            {
                Content = new StringContent(
                    body,
                    Encoding.UTF8,
                    "application/json")
            };

            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                options.SharedSecret);
            request.Headers.Add("Idempotency-Key", message.id.ToString());
            request.Headers.Add("X-Timestamp", timestamp);
            request.Headers.Add(
                "X-Signature",
                $"sha256={CreateSignature(timestamp, body)}");

            try
            {
                using var response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseContentRead,
                    cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(
                    cancellationToken);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return new DmsRegistrationDeliveryResult(
                        true,
                        false,
                        responseBody,
                        null);
                }

                return new DmsRegistrationDeliveryResult(
                    false,
                    (int)response.StatusCode >= 500 ||
                        response.StatusCode == HttpStatusCode.RequestTimeout,
                    null,
                    $"DMS a răspuns cu {(int)response.StatusCode}: " +
                    Trim(responseBody));
            }
            catch (HttpRequestException exception)
            {
                return new DmsRegistrationDeliveryResult(
                    false,
                    true,
                    null,
                    Trim(exception.Message));
            }
            catch (TaskCanceledException exception)
                when (!cancellationToken.IsCancellationRequested)
            {
                return new DmsRegistrationDeliveryResult(
                    false,
                    true,
                    null,
                    Trim(exception.Message));
            }
        }

        private string CreateSignature(string timestamp, string body)
        {
            using var hmac = new HMACSHA256(
                Encoding.UTF8.GetBytes(options.SharedSecret));

            return Convert.ToHexString(hmac.ComputeHash(
                Encoding.UTF8.GetBytes($"{timestamp}.{body}")))
                .ToLowerInvariant();
        }

        private static string Trim(string value)
        {
            return value.Length <= 1800 ? value : value[..1800];
        }
    }
}
