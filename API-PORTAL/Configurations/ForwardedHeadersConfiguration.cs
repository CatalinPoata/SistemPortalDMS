using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using System.Net;

namespace API_PORTAL.Configurations
{
    public sealed class ForwardedHeadersConfiguration
    : IConfigureOptions<ForwardedHeadersOptions>
    {
        private readonly IConfiguration configuration;

        public ForwardedHeadersConfiguration(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public void Configure(ForwardedHeadersOptions options)
        {
            var configuredAddress = configuration["ReverseProxy:KnownProxy"];

            if (string.IsNullOrWhiteSpace(configuredAddress))
            {
                return;
            }

            if (!IPAddress.TryParse(configuredAddress, out var proxyAddress))
            {
                throw new InvalidOperationException(
                    "ReverseProxy:KnownProxy trebuie să fie o adresă IP validă.");
            }

            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto;

            options.ForwardLimit = 1;

            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();

            options.KnownProxies.Add(proxyAddress);
            options.KnownProxies.Add(proxyAddress.MapToIPv6());
        }
    }
}
