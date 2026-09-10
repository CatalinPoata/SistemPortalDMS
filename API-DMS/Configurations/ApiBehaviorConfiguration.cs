using API_DMS.Errors;
using Microsoft.AspNetCore.Mvc;

namespace API_DMS.Configurations
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
