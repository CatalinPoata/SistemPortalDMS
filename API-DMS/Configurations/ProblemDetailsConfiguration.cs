using API_DMS.Errors;

namespace API_DMS.Configurations
{
    public static class ProblemDetailsConfiguration
    {
        public static void Configure(
            ProblemDetailsOptions options)
        {
            options.CustomizeProblemDetails =
                Customize;
        }

        private static void Customize(
            ProblemDetailsContext context)
        {
            var status =
                context.ProblemDetails.Status ??
                context.HttpContext.Response.StatusCode;

            ApiProblemDetails.EnsureFields(
                context.HttpContext,
                context.ProblemDetails,
                status);
        }
    }
}
