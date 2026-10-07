using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Statik.Server.Hosting;

public static class ProxyHeaders
{
    public static IServiceCollection AddProxyHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        // Parsed here so a bad address fails at startup.
        var proxies = (configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
            .Select(IPAddress.Parse)
            .ToArray();

        return services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Loopback is trusted for the Vite proxy. Any other proxy must be in KnownProxies.
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(proxy);
            }
        });
    }
}
