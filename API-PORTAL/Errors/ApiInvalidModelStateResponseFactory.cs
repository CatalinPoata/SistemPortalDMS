using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.Json;

namespace API_PORTAL.Errors
{
    public static class ApiInvalidModelStateResponseFactory
    {
        public static IActionResult Create(ActionContext context)
        {
            var invalidEntries = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToArray();

            var jsonEntries = invalidEntries
                .Where(entry => entry.Value!.Errors.Any(IsJsonError))
                .ToArray();

            var hasJsonErrors = jsonEntries.Length > 0;

            var selectedEntries = hasJsonErrors
                ? jsonEntries
                : invalidEntries;

            var errors = selectedEntries.ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(GetPublicMessage)
                    .ToArray());

            var status = hasJsonErrors
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status422UnprocessableEntity;

            var problem = ApiProblemDetails.CreateValidation(
                context.HttpContext,
                status,
                hasJsonErrors ? "JSON invalid" : "Date invalide",
                hasJsonErrors
                    ? "Corpul JSON nu respectă formatul sau contractul endpointului."
                    : "Unul sau mai multe câmpuri sunt invalide.",
                errors);

            return new ObjectResult(problem)
            {
                StatusCode = status,
                ContentTypes = { "application/problem+json" }
            };
        }

        private static bool IsJsonError(ModelError error)
        {
            for (var exception = error.Exception;
                 exception is not null;
                 exception = exception.InnerException)
            {
                if (exception is JsonException)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetPublicMessage(ModelError error)
        {
            if (IsJsonError(error))
            {
                return "JSON invalid, valoare incompatibilă sau câmp nepermis.";
            }

            return string.IsNullOrWhiteSpace(error.ErrorMessage)
                ? "Valoarea este invalidă."
                : error.ErrorMessage;
        }
    }
}
