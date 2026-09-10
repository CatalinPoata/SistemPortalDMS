using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace API_DMS.Integration
{
    public sealed class DownloadedPortalFile : IAsyncDisposable
    {
        private readonly HttpResponseMessage response;

        public DownloadedPortalFile(HttpResponseMessage response)
        {
            this.response = response;
            Stream = response.Content.ReadAsStream();
        }

        public Stream Stream { get; }

        public long? ContentLength => response.Content.Headers.ContentLength;

        public ValueTask DisposeAsync()
        {
            Stream.Dispose();
            response.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    public interface IPortalSubmissionFileClient
    {
        Task<DownloadedPortalFile> DownloadAsync(
            Guid fileId,
            CancellationToken cancellationToken);
    }

    public sealed class PortalSubmissionFileClient
        : IPortalSubmissionFileClient
    {
        private readonly HttpClient httpClient;
        private readonly PortalIntegrationOptions options;

        public PortalSubmissionFileClient(
            HttpClient httpClient,
            IOptions<PortalIntegrationOptions> options)
        {
            this.httpClient = httpClient;
            this.options = options.Value;
        }

        public async Task<DownloadedPortalFile> DownloadAsync(
            Guid fileId,
            CancellationToken cancellationToken)
        {
            var timestamp = DateTimeOffset.UtcNow.ToString(
                "O",
                CultureInfo.InvariantCulture);

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/internal/files/{fileId}/content");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                options.SharedSecret);
            request.Headers.Add("X-Timestamp", timestamp);
            request.Headers.Add(
                "X-Signature",
                $"sha256={CreateSignature(timestamp, string.Empty)}");

            var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                response.Dispose();
                throw new PortalIntegrationRuleException(
                    "Un fișier al cererii nu mai este disponibil în Portal.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                response.Dispose();
                throw new HttpRequestException(
                    $"Portalul a refuzat descărcarea fișierului ({status}).");
            }

            return new DownloadedPortalFile(response);
        }

        private string CreateSignature(string timestamp, string body)
        {
            using var hmac = new HMACSHA256(
                Encoding.UTF8.GetBytes(options.SharedSecret));

            return Convert.ToHexString(hmac.ComputeHash(
                Encoding.UTF8.GetBytes($"{timestamp}.{body}")))
                .ToLowerInvariant();
        }
    }
}
