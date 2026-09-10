using API_PORTAL.Errors;
using System.Diagnostics;

namespace API_PORTAL.Integration
{
    public sealed class TracePropagationHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly ILogger<TracePropagationHandler> logger;

        public TracePropagationHandler(
            IHttpContextAccessor httpContextAccessor,
            ILogger<TracePropagationHandler> logger)
        {
            this.httpContextAccessor = httpContextAccessor;
            this.logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var traceId = ResolveTraceId();

            if (!request.Headers.Contains("X-Trace-Id"))
            {
                request.Headers.TryAddWithoutValidation(
                    "X-Trace-Id",
                    traceId);
            }

            using (logger.BeginScope(new Dictionary<string, object?>
            {
                ["traceId"] = traceId,
                ["integrationTarget"] = "DMS",
                ["integrationMethod"] = request.Method.Method,
                ["integrationPath"] = request.RequestUri?.PathAndQuery
            }))
            {
                logger.LogInformation(
                    "Se trimite un apel de integrare către DMS.");

                var response = await base.SendAsync(
                    request,
                    cancellationToken);

                logger.LogInformation(
                    "Apelul de integrare către DMS s-a încheiat cu {StatusCode}.",
                    (int)response.StatusCode);

                return response;
            }
        }

        private string ResolveTraceId()
        {
            var current = httpContextAccessor.HttpContext;

            if (current?.Items.TryGetValue(
                    TraceIdMiddleware.ItemKey,
                    out var value) == true &&
                value is string traceId &&
                !string.IsNullOrWhiteSpace(traceId))
            {
                return traceId;
            }

            var activityTraceId = Activity.Current?.TraceId;
            return activityTraceId is not null && activityTraceId.Value != default
                ? activityTraceId.Value.ToHexString()
                : ActivityTraceId.CreateRandom().ToHexString();
        }
    }
}
