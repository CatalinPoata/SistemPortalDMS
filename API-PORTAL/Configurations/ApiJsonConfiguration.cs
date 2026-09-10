using System.Text.Json.Serialization;

namespace API_PORTAL.Configurations
{
    public static class ApiJsonConfiguration
    {
        public static void Configure(
            Microsoft.AspNetCore.Mvc.JsonOptions options)
        {
            options.AllowInputFormatterExceptionMessages = false;

            options.JsonSerializerOptions.UnmappedMemberHandling =
                JsonUnmappedMemberHandling.Disallow;

            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter());
        }
    }
}
