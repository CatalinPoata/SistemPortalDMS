using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;

namespace API_DMS.Errors
{
    public sealed class ProblemDetailsResultFilter
    : IAsyncResultFilter
    {
        public async Task OnResultExecutionAsync(
            ResultExecutingContext context,
            ResultExecutionDelegate next)
        {
            if (context.Result is ObjectResult objectResult)
            {
                if (objectResult.Value is ProblemDetails problem)
                {
                    var status =
                        objectResult.StatusCode ??
                        problem.Status ??
                        context.HttpContext.Response.StatusCode;

                    ApiProblemDetails.EnsureFields(
                        context.HttpContext,
                        problem,
                        status);

                    objectResult.StatusCode =
                        problem.Status;

                    objectResult.ContentTypes.Clear();
                    objectResult.ContentTypes.Add(
                        "application/problem+json");
                }
                else
                {
                    var errorProperty = objectResult.Value?
                        .GetType()
                        .GetProperty(
                            "error",
                            BindingFlags.Public |
                            BindingFlags.Instance |
                            BindingFlags.IgnoreCase);

                    var errorMessage =
                        errorProperty?.GetValue(objectResult.Value)?
                        .ToString();

                    if (!string.IsNullOrWhiteSpace(errorMessage))
                    {
                        var status =
                            objectResult.StatusCode ??
                            StatusCodes.Status500InternalServerError;

                        var problemDetails =
                            ApiProblemDetails.Create(
                                context.HttpContext,
                                status,
                                ApiProblemDetails.TitleForStatus(status),
                                errorMessage);

                        context.Result = new ObjectResult(problemDetails)
                        {
                            StatusCode = status,
                            ContentTypes =
                        {
                            "application/problem+json"
                        }
                        };
                    }
                }
            }

            await next();
        }
    }
}
