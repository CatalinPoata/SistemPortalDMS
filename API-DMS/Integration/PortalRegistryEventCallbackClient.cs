using API_DMS.Entities;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace API_DMS.Integration
{
    public sealed record PortalCallbackDeliveryResult(
        bool Delivered,
        bool Retryable,
        string? Error);

    public interface IPortalRegistryEventCallbackClient
    {
        Task<PortalCallbackDeliveryResult> DeliverAsync(
            OutboxMessage message,
            CancellationToken cancellationToken);
    }

    public sealed class PortalRegistryEventCallbackClient
        : IPortalRegistryEventCallbackClient
    {
        private readonly HttpClient httpClient;
        private readonly PortalIntegrationOptions options;

        public PortalRegistryEventCallbackClient(
            HttpClient httpClient,
            IOptions<PortalIntegrationOptions> options)
        {
            this.httpClient = httpClient;
            this.options = options.Value;
        }

        public async Task<PortalCallbackDeliveryResult> DeliverAsync(
            OutboxMessage message,
            CancellationToken cancellationToken)
        {
            var body = message.payload.RootElement.GetRawText();
            var timestamp = DateTimeOffset.UtcNow.ToString(
                "O",
                CultureInfo.InvariantCulture);

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "api/callbacks/registry-events")
            {
                Content = new StringContent(
                    body,
                    Encoding.UTF8,
                    "application/json")
            };

            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                options.SharedSecret);
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
                    return new PortalCallbackDeliveryResult(
                        true,
                        false,
                        null);
                }

                return new PortalCallbackDeliveryResult(
                    false,
                    true,
                    $"Portal a răspuns cu {(int)response.StatusCode}: " +
                    Trim(responseBody));
            }
            catch (HttpRequestException exception)
            {
                return new PortalCallbackDeliveryResult(
                    false,
                    true,
                    Trim(exception.Message));
            }
            catch (TaskCanceledException exception)
                when (!cancellationToken.IsCancellationRequested)
            {
                return new PortalCallbackDeliveryResult(
                    false,
                    true,
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
