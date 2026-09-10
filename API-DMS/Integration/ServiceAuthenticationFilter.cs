using API_DMS.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace API_DMS.Integration
{
    public sealed class ServiceAuthenticationFilter
        : IAsyncAuthorizationFilter
    {
        public const string RawBodyItemKey =
            "dms.integration.raw-request-body";

        private static readonly TimeSpan TimestampTolerance =
            TimeSpan.FromMinutes(5);

        private readonly PortalIntegrationOptions options;

        public ServiceAuthenticationFilter(
            IOptions<PortalIntegrationOptions> options)
        {
            this.options = options.Value;
        }

        public async Task OnAuthorizationAsync(
            AuthorizationFilterContext context)
        {
            var request = context.HttpContext.Request;
            var authorization = request.Headers.Authorization
                .FirstOrDefault();
            var timestamp = request.Headers["X-Timestamp"]
                .FirstOrDefault();
            var signature = request.Headers["X-Signature"]
                .FirstOrDefault();

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

            var candidate = authorization[prefix.Length..].Trim();

            return FixedTimeEquals(candidate, options.SharedSecret);
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

            byte[] received;

            try
            {
                received = Convert.FromHexString(
                    receivedSignature[prefix.Length..]);
            }
            catch (FormatException)
            {
                return false;
            }

            using var hmac = new HMACSHA256(
                Encoding.UTF8.GetBytes(options.SharedSecret));

            var expected = hmac.ComputeHash(
                Encoding.UTF8.GetBytes($"{timestamp}.{body}"));

            return CryptographicOperations.FixedTimeEquals(
                received,
                expected);
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

        private static bool FixedTimeEquals(
            string first,
            string second)
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
    public sealed class ServiceAuthenticationAttribute
        : TypeFilterAttribute
    {
        public ServiceAuthenticationAttribute()
            : base(typeof(ServiceAuthenticationFilter))
        {
        }
    }
}
