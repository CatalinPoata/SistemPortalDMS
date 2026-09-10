using API_DMS.DTO.RegistryEntries;
using API_DMS.Errors;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API_DMS.Filters
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RejectUnknownUploadFieldsAttribute
    : ActionFilterAttribute
    {
        private static readonly HashSet<string> AllowedFields =
            typeof(UploadRegistryDocumentRequest)
                .GetProperties()
                .Select(property => property.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        public RejectUnknownUploadFieldsAttribute()
        {
            Order = -3000;
        }

        public override void OnActionExecuting(
            ActionExecutingContext context)
        {
            var form = context.HttpContext.Features
                .Get<IFormFeature>()?.Form;

            if (form is null)
            {
                return;
            }

            var errors = form.Keys
                .Concat(form.Files.Select(file => file.Name))
                .Where(name => !AllowedFields.Contains(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    name => name,
                    _ => new[] { "Câmp nepermis pentru acest endpoint." });

            if (errors.Count == 0)
            {
                return;
            }

            var problem = ApiProblemDetails.CreateValidation(
                context.HttpContext,
                StatusCodes.Status400BadRequest,
                "Câmpuri nepermise",
                "Formularul conține câmpuri care nu sunt acceptate.",
                errors);

            context.Result = new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = { "application/problem+json" }
            };
        }
    }
}
