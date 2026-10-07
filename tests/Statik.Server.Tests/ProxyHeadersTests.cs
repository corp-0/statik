using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Statik.Server.Hosting;

namespace Statik.Server.Tests;

public class ProxyHeadersTests
{
    [Theory]
    [InlineData("10.213.0.2", "https 203.0.113.7")]
    [InlineData("127.0.0.1", "https 203.0.113.7")]
    [InlineData("10.213.0.3", "http 10.213.0.3")]
    public async Task ForwardedHeadersAreTrustedOnlyFromKnownProxies(string remote, string expected)
    {
        using var server = new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
            .ConfigureServices(services => services.AddProxyHeaders(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["ForwardedHeaders:KnownProxies:0"] = "10.213.0.2" })
                .Build()))
            .Configure(app =>
            {
                app.Use(async (context, next) =>
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
                    await next(context);
                });
                app.UseForwardedHeaders();
                app.Run(context => context.Response.WriteAsync($"{context.Request.Scheme} {context.Connection.RemoteIpAddress}"));
            })).Start();
        using var client = server.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");

        var response = await client.SendAsync(request);

        Assert.Equal(expected, await response.Content.ReadAsStringAsync());
    }
}
