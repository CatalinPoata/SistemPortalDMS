using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace API_PORTAL.Integration
{
    public enum DmsResponseDocumentLookupStatus
    {
        Found,
        NotFound,
        Unavailable
    }

    public sealed record DmsResponseDocumentDownloadUrl(
        Guid DocumentId,
        string Url,
        DateTimeOffset ExpiresAt);

    public sealed record DmsResponseDocumentLookupResult(
        DmsResponseDocumentLookupStatus Status,
        DmsResponseDocumentDownloadUrl? Download,
        string? Error);

    public interface IDmsResponseDocumentClient
    {
        Task<DmsResponseDocumentLookupResult> CreateDownloadUrlAsync(
            Guid documentId,
            CancellationToken cancellationToken);
    }

    public sealed class DmsResponseDocumentClient
        : IDmsResponseDocumentClient
    {
        private readonly HttpClient httpClient;
        private readonly DmsIntegrationOptions options;

        public DmsResponseDocumentClient(
            HttpClient httpClient,
            IOptions<DmsIntegrationOptions> options)
        {
            this.httpClient = httpClient;
            this.options = options.Value;
        }

        public async Task<DmsResponseDocumentLookupResult>
            CreateDownloadUrlAsync(
                Guid documentId,
                CancellationToken cancellationToken)
        {
            const string body = "";
            var timestamp = DateTimeOffset.UtcNow.ToString(
                "O",
                CultureInfo.InvariantCulture);

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"api/internal/documents/{documentId}/download-url")
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

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return new DmsResponseDocumentLookupResult(
                        DmsResponseDocumentLookupStatus.NotFound,
                        null,
                        null);
                }

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    var error = await response.Content.ReadAsStringAsync(
                        cancellationToken);
                    return new DmsResponseDocumentLookupResult(
                        DmsResponseDocumentLookupStatus.Unavailable,
                        null,
                        $"DMS a răspuns cu {(int)response.StatusCode}: " +
                        Trim(error));
                }

                var responseBody = await response.Content
                    .ReadFromJsonAsync<DmsResponseDocumentDownloadUrl>(
                        new JsonSerializerOptions(JsonSerializerDefaults.Web),
                        cancellationToken);

                if (responseBody is null ||
                    responseBody.DocumentId != documentId ||
                    string.IsNullOrWhiteSpace(responseBody.Url))
                {
                    return new DmsResponseDocumentLookupResult(
                        DmsResponseDocumentLookupStatus.Unavailable,
                        null,
                        "DMS a trimis un URL semnat invalid.");
                }

                return new DmsResponseDocumentLookupResult(
                    DmsResponseDocumentLookupStatus.Found,
                    responseBody,
                    null);
            }
            catch (HttpRequestException exception)
            {
                return new DmsResponseDocumentLookupResult(
                    DmsResponseDocumentLookupStatus.Unavailable,
                    null,
                    Trim(exception.Message));
            }
            catch (TaskCanceledException exception)
                when (!cancellationToken.IsCancellationRequested)
            {
                return new DmsResponseDocumentLookupResult(
                    DmsResponseDocumentLookupStatus.Unavailable,
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
