using API_DMS.Integration;
using System.Net;
using System.Net.Http.Headers;

namespace API_DMS_TESTS.Infrastructure
{
    public sealed class FakePortalSubmissionFileClient
        : IPortalSubmissionFileClient
    {
        private readonly Dictionary<Guid, (byte[] Content, string ContentType)>
            files = [];

        public void SetFile(Guid fileId, byte[] content, string contentType)
        {
            files[fileId] = (content, contentType);
        }

        public Task<DownloadedPortalFile> DownloadAsync(
            Guid fileId,
            CancellationToken cancellationToken)
        {
            if (!files.TryGetValue(fileId, out var file))
            {
                throw new PortalIntegrationRuleException(
                    "Fișierul de test nu există.");
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(file.Content)
            };
            response.Content.Headers.ContentType =
                MediaTypeHeaderValue.Parse(file.ContentType);
            response.Content.Headers.ContentLength = file.Content.LongLength;

            return Task.FromResult(new DownloadedPortalFile(response));
        }
    }
}
