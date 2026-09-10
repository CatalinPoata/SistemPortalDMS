using API_PORTAL.Errors;
using Microsoft.AspNetCore.Mvc;

namespace API_PORTAL.Configurations
{
    public static class ApiBehaviorConfiguration
    {
        public static void Configure(
            ApiBehaviorOptions options)
        {
            options.InvalidModelStateResponseFactory =
                ApiInvalidModelStateResponseFactory.Create;
        }
    }
}
