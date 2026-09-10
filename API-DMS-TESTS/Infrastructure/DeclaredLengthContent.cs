using System.Net;
using System.Net.Http.Headers;


namespace API_DMS_TESTS.Infrastructure
{
    public sealed class DeclaredLengthContent
    : HttpContent
    {
        private readonly long declaredLength;

        public DeclaredLengthContent(
            long declaredLength)
        {
            this.declaredLength =
                declaredLength;

            Headers.ContentType =
                MediaTypeHeaderValue.Parse(
                    "multipart/form-data; " +
                    "boundary=TestBoundary");
        }

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context)
        {
            return Task.CompletedTask;
        }

        protected override bool TryComputeLength(
            out long length)
        {
            length = declaredLength;
            return true;
        }
    }
}
