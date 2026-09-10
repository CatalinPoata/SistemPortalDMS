using API_DMS.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using API_DMS.Errors;

namespace API_DMS.Filters
{
    public sealed class UploadSizeFilter
    : IResourceFilter, IOrderedFilter
    {
        public int Order => int.MinValue;

        public void OnResourceExecuting(
            ResourceExecutingContext context)
        {
            var request = context.HttpContext.Request;

            if (!IsRegistryDocumentUpload(request))
            {
                return;
            }

            var maxRequestSize =
                FileStorageService.MaxUploadRequestSize;

            if (!(request.ContentLength > maxRequestSize))
            {
                return;
            }

            var problem =
                ApiProblemDetails.Create(
                    context.HttpContext,
                    StatusCodes.Status413PayloadTooLarge,
                    "Fișier prea mare",
                    "Dimensiunea maximă permisă pentru un fișier este de 10 MB.");

            context.Result = new ObjectResult(problem)
            {
                StatusCode =
                    StatusCodes.Status413PayloadTooLarge,

                ContentTypes =
    {
        "application/problem+json"
    }
            };
        }

        public void OnResourceExecuted(
            ResourceExecutedContext context)
        {
        }

        private static bool IsRegistryDocumentUpload(
            HttpRequest request)
        {
            if (!HttpMethods.IsPost(request.Method))
            {
                return false;
            }

            var path = request.Path.Value?.TrimEnd('/');

            return path is not null &&
                   path.StartsWith(
                       "/api/registry-entries/",
                       StringComparison.OrdinalIgnoreCase) &&
                   path.EndsWith(
                       "/documents",
                       StringComparison.OrdinalIgnoreCase);
        }
    }
}
