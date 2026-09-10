using System.Diagnostics;

namespace API_PORTAL.Errors
{
    public sealed class TraceIdMiddleware
    {
        public const string ItemKey = "__TraceId";

        private readonly RequestDelegate next;
        private readonly ILogger<TraceIdMiddleware> logger;

        public TraceIdMiddleware(
            RequestDelegate next,
            ILogger<TraceIdMiddleware> logger)
        {
            this.next = next;
            this.logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var incomingTraceId =
                context.Request.Headers["X-Trace-Id"]
                    .FirstOrDefault();

            var traceId = IsValidTraceId(incomingTraceId)
                ? incomingTraceId!
                : GetActivityTraceId()
                  ?? ActivityTraceId.CreateRandom().ToHexString();

            context.Items[ItemKey] = traceId;
            context.TraceIdentifier = traceId;

            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Trace-Id"] = traceId;
                return Task.CompletedTask;
            });

            using (logger.BeginScope(new Dictionary<string, object?>
            {
                ["traceId"] = traceId,
                ["requestMethod"] = context.Request.Method,
                ["requestPath"] = context.Request.Path.Value
            }))
            {
                logger.LogInformation("A început procesarea cererii HTTP.");

                try
                {
                    await next(context);
                }
                finally
                {
                    logger.LogInformation(
                        "Procesarea cererii HTTP s-a încheiat cu {StatusCode}.",
                        context.Response.StatusCode);
                }
            }
        }

        private static string? GetActivityTraceId()
        {
            var traceId = Activity.Current?.TraceId;

            return traceId is not null &&
                   traceId.Value != default
                ? traceId.Value.ToHexString()
                : null;
        }

        private static bool IsValidTraceId(string? value)
        {
            return value is not null &&
                   value.Length == 32 &&
                   value.Any(character => character != '0') &&
                   value.All(Uri.IsHexDigit);
        }
    }
}
