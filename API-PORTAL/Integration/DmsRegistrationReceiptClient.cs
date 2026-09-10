using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace API_PORTAL.Integration
{
    public enum DmsReceiptDownloadStatus
    {
        Found,
        NotFound,
        Unavailable
    }

    public sealed record DmsReceiptDownloadResult(
        DmsReceiptDownloadStatus Status,
        byte[]? Content,
        string? Error);

    public interface IDmsRegistrationReceiptClient
    {
        Task<DmsReceiptDownloadResult> DownloadAsync(
            Guid entryId,
            CancellationToken cancellationToken);
    }

    public sealed class DmsRegistrationReceiptClient
        : IDmsRegistrationReceiptClient
    {
        private const int MaximumReceiptSize = 10 * 1024 * 1024;

        private readonly HttpClient httpClient;
        private readonly DmsIntegrationOptions options;

        public DmsRegistrationReceiptClient(
            HttpClient httpClient,
            IOptions<DmsIntegrationOptions> options)
        {
            this.httpClient = httpClient;
            this.options = options.Value;
        }

        public async Task<DmsReceiptDownloadResult> DownloadAsync(
            Guid entryId,
            CancellationToken cancellationToken)
        {
            const string body = "";
            var timestamp = DateTimeOffset.UtcNow.ToString(
                "O",
                CultureInfo.InvariantCulture);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/integration/registry-entries/{entryId}/receipt");

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
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return new DmsReceiptDownloadResult(
                        DmsReceiptDownloadStatus.NotFound,
                        null,
                        null);
                }

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    var error = await response.Content.ReadAsStringAsync(
                        cancellationToken);
                    return new DmsReceiptDownloadResult(
                        DmsReceiptDownloadStatus.Unavailable,
                        null,
                        $"DMS a răspuns cu {(int)response.StatusCode}: " +
                        Trim(error));
                }

                if (response.Content.Headers.ContentLength is > MaximumReceiptSize)
                {
                    return new DmsReceiptDownloadResult(
                        DmsReceiptDownloadStatus.Unavailable,
                        null,
                        "DMS a trimis o dovadă PDF prea mare.");
                }

                if (!string.Equals(
                        response.Content.Headers.ContentType?.MediaType,
                        "application/pdf",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return new DmsReceiptDownloadResult(
                        DmsReceiptDownloadStatus.Unavailable,
                        null,
                        "DMS nu a trimis un document PDF.");
                }

                var content = await response.Content.ReadAsByteArrayAsync(
                    cancellationToken);
                if (content.Length > MaximumReceiptSize ||
                    content.Length < 5 ||
                    !Encoding.ASCII.GetString(content, 0, 5).Equals(
                        "%PDF-",
                        StringComparison.Ordinal))
                {
                    return new DmsReceiptDownloadResult(
                        DmsReceiptDownloadStatus.Unavailable,
                        null,
                        "DMS a trimis un PDF invalid.");
                }

                return new DmsReceiptDownloadResult(
                    DmsReceiptDownloadStatus.Found,
                    content,
                    null);
            }
            catch (HttpRequestException exception)
            {
                return new DmsReceiptDownloadResult(
                    DmsReceiptDownloadStatus.Unavailable,
                    null,
                    Trim(exception.Message));
            }
            catch (TaskCanceledException exception)
                when (!cancellationToken.IsCancellationRequested)
            {
                return new DmsReceiptDownloadResult(
                    DmsReceiptDownloadStatus.Unavailable,
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
