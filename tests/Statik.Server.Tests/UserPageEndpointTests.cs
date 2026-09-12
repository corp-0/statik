using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Statik.Server.Data;
using Statik.Server.Endpoints;
using Statik.Server.Models;
using Statik.Server.Rendering;

namespace Statik.Server.Tests;

public class UserPageEndpointTests
{
    [Theory]
    [InlineData("<main>Hello {{ profile.display_name }}</main>", HttpStatusCode.OK)]
    [InlineData("{{ if }}", HttpStatusCode.InternalServerError)]
    [InlineData("{{ profile.secret }}", HttpStatusCode.InternalServerError)]
    [InlineData("{{ while true }}x{{ end }}", HttpStatusCode.InternalServerError)]
    public async Task AppliesSecurityHeadersAndHandlesRenderFailures(string body, HttpStatusCode status)
    {
        using var server = CreateServer();
        await SeedPage(server, body);
        using var client = server.GetTestClient();

        using var response = await client.GetAsync("/gilles");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(status, response.StatusCode);
        var policy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        foreach (var directive in new[]
                 {
                     "default-src 'none'", "script-src 'none'", "style-src 'unsafe-inline'",
                     "img-src 'self' https:", "font-src 'self'", "connect-src 'none'",
                     "object-src 'none'", "frame-src 'none'", "base-uri 'none'",
                     "form-action 'none'", "frame-ancestors 'none'", "sandbox allow-same-origin"
                 })
        {
            Assert.Contains(directive, policy.Split(';', StringSplitOptions.TrimEntries));
        }
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));

        if (status == HttpStatusCode.OK)
        {
            Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
            Assert.Equal("utf-8", response.Content.Headers.ContentType.CharSet);
            Assert.Contains("<main>Hello Gilles</main>", text);
        }
        else
        {
            Assert.Equal("This page could not be displayed.", text);
            Assert.True(response.Headers.CacheControl!.NoStore);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingUserOrPageReturns404(bool existingUser)
    {
        using var server = CreateServer();
        if (existingUser)
        {
            await SeedPage(server, null);
        }
        using var client = server.GetTestClient();

        using var response = await client.GetAsync("/gilles");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DoesNotServeUserPagesOnTheAppHost()
    {
        using var server = CreateServer();
        await SeedPage(server, "<main>Hello</main>");
        using var client = server.GetTestClient();
        client.BaseAddress = new Uri("https://app.statik.club");

        using var response = await client.GetAsync("/gilles");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
    }

    private static IHost CreateServer()
    {
        var databaseName = Guid.NewGuid().ToString();
        return new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddDbContext<StatikDbContext>(options => options.UseInMemoryDatabase(databaseName));
                services.AddIdentityCore<User>().AddEntityFrameworkStores<StatikDbContext>();
                services.AddSingleton<UserPageRenderer>();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapUserPageEndpoints());
            })).Start();
    }

    private static async Task SeedPage(IHost server, string? body)
    {
        using var scope = server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StatikDbContext>();
        var user = new User { UserName = "gilles", NormalizedUserName = "GILLES", DisplayName = "Gilles" };
        db.Users.Add(user);
        if (body is not null)
        {
            db.UserPages.Add(new UserPage { UserId = user.Id, Owner = user, BodyTemplate = body, Css = "" });
        }
        await db.SaveChangesAsync();
    }
}
