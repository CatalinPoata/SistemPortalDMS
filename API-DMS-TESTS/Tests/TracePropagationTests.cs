using API_DMS.Errors;
using API_DMS.Integration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using Xunit;

namespace API_DMS_TESTS.Tests;

public sealed class TracePropagationTests
{
    private const string TraceId = "2d9b5103a438b3c53d86c1d5f3b1d6a9";

    [Fact]
    public async Task Dms_to_portal_client_propagates_current_trace_id()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        accessor.HttpContext.Items[TraceIdMiddleware.ItemKey] = TraceId;

        var capture = new CaptureHandler();
        using var handler = new TracePropagationHandler(
            accessor,
            NullLogger<TracePropagationHandler>.Instance)
        {
            InnerHandler = capture
        };
        using var client = new HttpClient(handler);

        using var response = await client.GetAsync(
            "https://portal.test/api/ping",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TraceId, capture.TraceId);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? TraceId { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            TraceId = request.Headers.GetValues("X-Trace-Id").Single();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
