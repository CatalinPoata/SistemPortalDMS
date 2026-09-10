using API_PORTAL.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace API_PORTAL.Integration
{
    public sealed class ServiceAuthenticationFilter
        : IAsyncAuthorizationFilter
    {
        public const string RawBodyItemKey =
            "portal.integration.raw-request-body";

        private static readonly TimeSpan TimestampTolerance =
            TimeSpan.FromMinutes(5);

        private readonly DmsIntegrationOptions options;

        public ServiceAuthenticationFilter(
            IOptions<DmsIntegrationOptions> options)
        {
            this.options = options.Value;
        }

        public async Task OnAuthorizationAsync(
            AuthorizationFilterContext context)
        {
            var request = context.HttpContext.Request;
            var authorization = request.Headers.Authorization.FirstOrDefault();
            var timestamp = request.Headers["X-Timestamp"].FirstOrDefault();
            var signature = request.Headers["X-Signature"].FirstOrDefault();

            if (!HasExpectedBearer(authorization) ||
                !IsCurrentTimestamp(timestamp))
            {
                Reject(context);
                return;
            }

            var body = await ReadBodyAsync(
                request,
                context.HttpContext.RequestAborted);

            if (!HasExpectedSignature(timestamp!, body, signature))
            {
                Reject(context);
                return;
            }

            context.HttpContext.Items[RawBodyItemKey] = body;
        }

        private bool HasExpectedBearer(string? authorization)
        {
            const string prefix = "Bearer ";

            if (string.IsNullOrWhiteSpace(authorization) ||
                !authorization.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return FixedTimeEquals(
                authorization[prefix.Length..].Trim(),
                options.SharedSecret);
        }

        private static bool IsCurrentTimestamp(string? timestamp)
        {
            if (string.IsNullOrWhiteSpace(timestamp) ||
                !DateTimeOffset.TryParse(
                    timestamp,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsed))
            {
                return false;
            }

            return (DateTimeOffset.UtcNow - parsed.ToUniversalTime())
                .Duration() <= TimestampTolerance;
        }

        private bool HasExpectedSignature(
            string timestamp,
            string body,
            string? receivedSignature)
        {
            const string prefix = "sha256=";

            if (string.IsNullOrWhiteSpace(receivedSignature) ||
                !receivedSignature.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                using var hmac = new HMACSHA256(
                    Encoding.UTF8.GetBytes(options.SharedSecret));
                var expected = hmac.ComputeHash(
                    Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
                var received = Convert.FromHexString(
                    receivedSignature[prefix.Length..]);

                return CryptographicOperations.FixedTimeEquals(
                    received,
                    expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static async Task<string> ReadBodyAsync(
            HttpRequest request,
            CancellationToken cancellationToken)
        {
            request.EnableBuffering();

            using var reader = new StreamReader(
                request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            var body = await reader.ReadToEndAsync(cancellationToken);
            request.Body.Position = 0;

            return body;
        }

        private static bool FixedTimeEquals(string first, string second)
        {
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(first),
                Encoding.UTF8.GetBytes(second));
        }

        private static void Reject(AuthorizationFilterContext context)
        {
            var problem = ApiProblemDetails.Create(
                context.HttpContext,
                StatusCodes.Status401Unauthorized,
                "Autentificare serviciu invalidă",
                "Cererea dintre servicii nu are o semnătură validă.");

            context.Result = new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                ContentTypes = { "application/problem+json" }
            };
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class ServiceAuthenticationAttribute : TypeFilterAttribute
    {
        public ServiceAuthenticationAttribute()
            : base(typeof(ServiceAuthenticationFilter))
        {
        }
    }
}
