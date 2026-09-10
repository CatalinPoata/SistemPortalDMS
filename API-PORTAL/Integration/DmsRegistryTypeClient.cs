using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace API_PORTAL.Integration
{
    public enum RegistryTypeLookupStatus
    {
        Found,
        NotFound,
        Unavailable
    }

    public sealed record RegistryTypeLookupResult(
        RegistryTypeLookupStatus Status,
        bool IsClosed = false)
    {
        public static RegistryTypeLookupResult Missing { get; } =
            new(RegistryTypeLookupStatus.NotFound);

        public static RegistryTypeLookupResult Unavailable { get; } =
            new(RegistryTypeLookupStatus.Unavailable);
    }

    public sealed record DmsRegistryTypeListItem(
        string Code,
        string Name,
        string Direction,
        bool IsClosed);

    public sealed record RegistryTypeListResult(
        bool IsAvailable,
        IReadOnlyList<DmsRegistryTypeListItem> Items)
    {
        public static RegistryTypeListResult Unavailable { get; } =
            new(false, []);
    }

    public interface IDmsRegistryTypeClient
    {
        Task<RegistryTypeListResult> GetAllAsync(
            CancellationToken cancellationToken);

        Task<RegistryTypeLookupResult> GetAsync(
            string code,
            CancellationToken cancellationToken);
    }

    public sealed class DmsRegistryTypeClient
        : IDmsRegistryTypeClient
    {
        private readonly HttpClient httpClient;
        private readonly DmsIntegrationOptions options;

        public DmsRegistryTypeClient(
            HttpClient httpClient,
            IOptions<DmsIntegrationOptions> options)
        {
            this.httpClient = httpClient;
            this.options = options.Value;
        }

        public async Task<RegistryTypeListResult> GetAllAsync(
            CancellationToken cancellationToken)
        {
            var timestamp = DateTimeOffset.UtcNow.ToString(
                "O",
                CultureInfo.InvariantCulture);

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "api/integration/registry-types");

            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                options.SharedSecret);
            request.Headers.Add("X-Timestamp", timestamp);
            request.Headers.Add(
                "X-Signature",
                $"sha256={CreateSignature(timestamp, string.Empty)}");

            try
            {
                using var response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return RegistryTypeListResult.Unavailable;
                }

                var registries = await response.Content
                    .ReadFromJsonAsync<IReadOnlyList<RegistryTypeResponse>>(
                        cancellationToken: cancellationToken);

                if (registries is null)
                {
                    return RegistryTypeListResult.Unavailable;
                }

                return new RegistryTypeListResult(
                    true,
                    registries
                        .Select(item => new DmsRegistryTypeListItem(
                            item.Code,
                            item.Name,
                            item.Direction,
                            item.IsClosed))
                        .ToArray());
            }
            catch (HttpRequestException)
            {
                return RegistryTypeListResult.Unavailable;
            }
            catch (TaskCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                return RegistryTypeListResult.Unavailable;
            }
        }

        public async Task<RegistryTypeLookupResult> GetAsync(
            string code,
            CancellationToken cancellationToken)
        {
            var timestamp = DateTimeOffset.UtcNow.ToString(
                "O",
                CultureInfo.InvariantCulture);

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/integration/registry-types/{Uri.EscapeDataString(code)}");

            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                options.SharedSecret);
            request.Headers.Add("X-Timestamp", timestamp);
            request.Headers.Add(
                "X-Signature",
                $"sha256={CreateSignature(timestamp, string.Empty)}");

            try
            {
                using var response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return RegistryTypeLookupResult.Missing;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return RegistryTypeLookupResult.Unavailable;
                }

                var registry = await response.Content
                    .ReadFromJsonAsync<RegistryTypeResponse>(
                        cancellationToken: cancellationToken);

                return registry is null
                    ? RegistryTypeLookupResult.Unavailable
                    : new RegistryTypeLookupResult(
                        RegistryTypeLookupStatus.Found,
                        registry.IsClosed);
            }
            catch (HttpRequestException)
            {
                return RegistryTypeLookupResult.Unavailable;
            }
            catch (TaskCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                return RegistryTypeLookupResult.Unavailable;
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

        private sealed record RegistryTypeResponse(
            string Code,
            string Name,
            string Direction,
            bool IsClosed);
    }
}
