using Microsoft.AspNetCore.Mvc;

namespace API_DMS.Errors
{
    public static class ApiProblemDetails
    {
        public static ProblemDetails Create(
            HttpContext context,
            int status,
            string title,
            string detail)
        {
            var problem = new ProblemDetails
            {
                Type = TypeForStatus(status),
                Title = title,
                Status = status,
                Detail = detail,
                Instance = context.Request.Path
            };

            problem.Extensions["traceId"] = GetTraceId(context);

            return problem;
        }

        public static ValidationProblemDetails CreateValidation(
            HttpContext context,
            int status,
            string title,
            string detail,
            IDictionary<string, string[]> errors)
        {
            var problem = new ValidationProblemDetails(errors)
            {
                Type = TypeForStatus(status),
                Title = title,
                Status = status,
                Detail = detail,
                Instance = context.Request.Path
            };

            problem.Extensions["traceId"] = GetTraceId(context);

            return problem;
        }

        public static void EnsureFields(
            HttpContext context,
            ProblemDetails problem,
            int status)
        {
            var effectiveStatus = problem.Status ?? status;

            if (effectiveStatus < 400)
            {
                effectiveStatus = 500;
            }

            problem.Type ??= TypeForStatus(effectiveStatus);
            problem.Title ??= TitleForStatus(effectiveStatus);
            problem.Status ??= effectiveStatus;
            problem.Detail ??= "A apărut o eroare la procesarea cererii.";
            problem.Instance ??= context.Request.Path;

            problem.Extensions["traceId"] = GetTraceId(context);
        }

        public static string GetTraceId(HttpContext context)
        {
            if (context.Items.TryGetValue(
                    TraceIdMiddleware.ItemKey,
                    out var value) &&
                value is string traceId &&
                !string.IsNullOrWhiteSpace(traceId))
            {
                return traceId;
            }

            return context.TraceIdentifier;
        }

        public static string TypeForStatus(int status)
        {
            return status switch
            {
                400 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
                401 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.2",
                403 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.4",
                404 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.5",
                409 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.10",
                413 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.14",
                422 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.21",
                500 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1",
                503 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.4",
                _ => "about:blank"
            };
        }

        public static string TitleForStatus(int status)
        {
            return status switch
            {
                400 => "Cerere invalidă",
                401 => "Autentificare necesară",
                403 => "Acces interzis",
                404 => "Resursă inexistentă",
                409 => "Conflict",
                413 => "Payload prea mare",
                422 => "Regulă de business încălcată",
                500 => "Eroare internă",
                503 => "Serviciu indisponibil",
                _ => "Eroare"
            };
        }
    }
}
