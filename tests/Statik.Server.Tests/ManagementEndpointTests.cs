using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Statik.Server.Data;
using Statik.Server.Endpoints;
using Statik.Server.Identity;
using Statik.Server.Rendering;
using Statik.Server.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Statik.Server.Models;

namespace Statik.Server.Tests;

public class ManagementEndpointTests
{
    [Fact]
    public async Task SignupPageCrudAndLoginRoundTrip()
    {
        using var server = CreateServer();
        using var client = CreateClient(server);
        await SignUp(server, client, "alice");

        var profile = await client.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal("alice", profile.GetProperty("username").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/me/page")).StatusCode);

        await Csrf(client);
        var source = new { bodyTemplate = "<h1>{{ profile.display_name }}</h1><script>alert(1)</script>", css = "h1 { color: red; }" };
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/me/page", source)).StatusCode);
        var stored = await client.GetFromJsonAsync<JsonElement>("/api/me/page");
        Assert.Equal(source.bodyTemplate, stored.GetProperty("bodyTemplate").GetString());
        var rendered = await client.GetStringAsync("https://statik.club/alice");
        Assert.Contains("<h1>alice</h1>", rendered);
        Assert.DoesNotContain("<script>", rendered);

        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/me/page", new { bodyTemplate = "Updated", css = "" })).StatusCode);
        Assert.Contains("Updated", await client.GetStringAsync("https://statik.club/alice"));

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        CaptureCookies(client, logout);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);

        await Csrf(client);
        var badLogin = await client.PostAsJsonAsync("/api/auth/login", new { email = "alice@example.test", password = "Wrong!123" });
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "alice@example.test", password = "Password!123" });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        CaptureCookies(client, login);

        await Csrf(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/me/page")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("https://statik.club/alice")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task SessionsOnlyAccessTheirOwnPages()
    {
        using var server = CreateServer();
        using var alice = CreateClient(server);
        using var bob = CreateClient(server);
        await SignUp(server, alice, "alice");
        await SignUp(server, bob, "bob");
        await Csrf(alice);
        await Csrf(bob);

        await alice.PutAsJsonAsync("/api/me/page", new { bodyTemplate = "Alice's page", css = "" });
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync("/api/me/page")).StatusCode);
        await bob.PutAsJsonAsync("/api/me/page", new { userId = "alice", bodyTemplate = "Bob's page", css = "" });
        await bob.DeleteAsync("/api/me/page");
        Assert.Contains("Alice's page", await alice.GetStringAsync("https://statik.club/alice"));
    }

    [Fact]
    public async Task MutationsRequireAuthenticationAndCsrf()
    {
        using var server = CreateServer();
        using var client = CreateClient(server);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me/page")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/me/page", new { bodyTemplate = "", css = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/me/page")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/signup",
            new { username = "alice", displayName = "Alice", email = "alice@example.test", password = "Password!123" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login",
            new { email = "alice@example.test", password = "Password!123" })).StatusCode);

        await SignUp(server, client, "alice");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/me/page", new { bodyTemplate = "", css = "" })).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync("/api/me/page")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
    }

    [Theory]
    [InlineData("statik.club")]
    [InlineData("untrusted.example")]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("app.statik.dev")]
    public async Task ManagementEndpointsAreUnavailableOnOtherHosts(string host)
    {
        using var server = CreateServer();
        using var client = CreateClient(server);
        client.BaseAddress = new Uri($"https://{host}");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/auth/csrf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/auth/signup",
            new { username = "alice", displayName = "Alice", email = "alice@example.test", password = "Password!123" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/me/page", new { bodyTemplate = "", css = "" })).StatusCode);
    }

    [Fact]
    public async Task AppHostUsesSecureHostOnlyCookies()
    {
        using var server = CreateServer(development: false);
        using var client = CreateClient(server);
        client.BaseAddress = new Uri("https://app.statik.club");
        await Csrf(client);
        await SignUp(server, client, "alice");
        await Csrf(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "alice@example.test", password = "Password!123" });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("__Host-statik_auth="));
        Assert.Contains("secure", cookie);
        Assert.Contains("httponly", cookie);
        Assert.Contains("samesite=strict", cookie);
        Assert.DoesNotContain("domain=", cookie);
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [Theory]
    [InlineData("api", "Alice", "Password!123")]
    [InlineData("bad.name", "Alice", "Password!123")]
    [InlineData("alice", " ", "Password!123")]
    [InlineData("alice", "Alice", "weak")]
    public async Task InvalidSignupIsRejected(string username, string displayName, string password)
    {
        using var server = CreateServer();
        using var client = CreateClient(server);
        await Csrf(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/signup", new { username, displayName, email = "alice@example.test", password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Theory]
    [InlineData(true, "a", HttpStatusCode.NoContent)]
    [InlineData(false, "a", HttpStatusCode.BadRequest)]
    [InlineData(true, "", HttpStatusCode.BadRequest)]
    [InlineData(false, "", HttpStatusCode.BadRequest)]
    public async Task SignupRelaxesPasswordRequirementsOnlyInDevelopment(bool development, string password, HttpStatusCode expected)
    {
        using var server = CreateServer(development);
        using var client = CreateClient(server);
        await Csrf(client);
        var response = await client.PostAsJsonAsync("/api/auth/signup",
            new { username = "alice", displayName = "Alice", email = "alice@example.test", password });
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task InvalidPageSourceDoesNotOverwriteSavedPage()
    {
        using var server = CreateServer();
        using var client = CreateClient(server);
        await SignUp(server, client, "alice");
        await Csrf(client);
        await client.PutAsJsonAsync("/api/me/page", new { bodyTemplate = "Original", css = "" });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/me/page", new { bodyTemplate = new string('x', 1_000_001), css = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/me/page", new { bodyTemplate = "", css = new string('x', 1_000_001) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/me/page", new { css = "" })).StatusCode);
        Assert.Contains("Original", await client.GetStringAsync("https://statik.club/alice"));
    }

    [Fact]
    public async Task SignupRequiresEmailConfirmationAndLoginUsesEmailOnly()
    {
        using var server = CreateServer();
        using var client = CreateClient(server);
        await Csrf(client);
        var signup = await client.PostAsJsonAsync("/api/auth/signup",
            new { username = "alice", displayName = "Alice", email = "private@example.test", password = "Password!123" });
        Assert.Equal(HttpStatusCode.NoContent, signup.StatusCode);
        Assert.False(signup.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(cookie => cookie.StartsWith("__Host-statik_auth=")));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
            new { email = "private@example.test", password = "Password!123" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login",
            new { username = "alice", password = "Password!123" })).StatusCode);

        var email = (TestEmailSender)server.Services.GetRequiredService<IAccountEmailSender>();
        var parameters = EmailParameters(email.Messages.Single());
        Assert.StartsWith("https://app.statik.club/#/confirm-email?", email.Messages.Single().Split('\n').Single(line => line.StartsWith("https://")));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/confirm-email",
            new { email = "private@example.test", token = "invalid" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/confirm-email", parameters)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/login",
            new { email = "PRIVATE@example.test", password = "Password!123" })).StatusCode);
    }

    [Fact]
    public async Task ConfirmationCannotBeUsedForAnotherAccountOrAfterExpiry()
    {
        using var server = CreateServer();
        using var client = CreateClient(server);
        await Csrf(client);
        foreach (var username in new[] { "alice", "bob" })
        {
            await client.PostAsJsonAsync("/api/auth/signup", new { username, displayName = username, email = $"{username}@example.test", password = "Password!123" });
        }
        var emails = (TestEmailSender)server.Services.GetRequiredService<IAccountEmailSender>();
        var parameters = EmailParameters(emails.Messages.First());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/confirm-email",
            new { email = "bob@example.test", token = parameters["token"] })).StatusCode);
        server.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataProtectionTokenProviderOptions>>().Value.TokenLifespan = TimeSpan.FromSeconds(-1);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/confirm-email", parameters)).StatusCode);
    }

    [Theory]
    [InlineData(false, "ChangedPassword!123")]
    [InlineData(true, "a")]
    public async Task ResendAndResetAreGenericAndResetTokensAreSingleUse(bool development, string password)
    {
        using var server = CreateServer(development);
        using var client = CreateClient(server);
        await SignUp(server, client, "alice");
        await Csrf(client);
        var emails = (TestEmailSender)server.Services.GetRequiredService<IAccountEmailSender>();
        var count = emails.Messages.Count;
        foreach (var email in new[] { "alice@example.test", "unknown@example.test" })
        {
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/resend-confirmation", new { email })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email })).StatusCode);
        }
        Assert.Equal(count + 1, emails.Messages.Count);
        var parameters = EmailParameters(emails.Messages.Last());
        if (!development)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/reset-password",
                new { email = parameters["email"], token = parameters["token"], password = "a" })).StatusCode);
        }
        var reset = new { email = parameters["email"], token = parameters["token"], password };
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/reset-password", reset)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/reset-password", reset)).StatusCode);
        using var anonymous = CreateClient(server);
        await Csrf(anonymous);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "alice@example.test", password = "Password!123" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "alice@example.test", password = reset.password })).StatusCode);
    }

    [Fact]
    public async Task DuplicateEmailDoesNotCreateAnotherAccount()
    {
        using var server = CreateServer();
        using var client = CreateClient(server);
        await SignUp(server, client, "alice");
        await Csrf(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/signup",
            new { username = "bob", displayName = "Bob", email = "ALICE@example.test", password = "Password!123" })).StatusCode);
        using var scope = server.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<StatikDbContext>().Users.CountAsync());
    }

    [Fact]
    public async Task AuthenticationRequestsAreRateLimited()
    {
        using var server = CreateServer();
        using var client = CreateClient(server);
        await Csrf(client);
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.test", password = "Password!123" })).StatusCode);
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.test", password = "Password!123" })).StatusCode);
    }

    [Fact]
    public async Task DevelopmentAdminCreatesConfirmsEditsAndDeletesUsersWithoutMail()
    {
        using var server = CreateServer(development: true);
        using var client = CreateClient(server);
        await Csrf(client);
        var create = new { userName = "alice", displayName = "Alice", email = "alice@example.test", about = "Hello", emailConfirmed = true, password = "a" };
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/admin/users", create)).StatusCode);
        var users = await client.GetFromJsonAsync<JsonElement[]>("/api/admin/users");
        var id = Assert.Single(users!).GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/admin/users/{id}",
            new { userName = "renamed", displayName = "Renamed", email = "new@example.test", about = "Updated", emailConfirmed = true, password = "b" })).StatusCode);
        using var login = CreateClient(server);
        await Csrf(login);
        var response = await login.PostAsJsonAsync("/api/auth/login", new { email = "NEW@example.test", password = "b" });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        CaptureCookies(login, response);
        await Csrf(login);
        await login.PutAsJsonAsync("/api/me/page", new { bodyTemplate = "Page", css = "" });
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/admin/users/{id}")).StatusCode);
        Assert.Empty(await client.GetFromJsonAsync<JsonElement[]>("/api/admin/users") ?? []);
        Assert.Empty(((TestEmailSender)server.Services.GetRequiredService<IAccountEmailSender>()).Messages);
    }

    [Theory]
    [InlineData(false, "app.statik.club", "127.0.0.1", "https://app.statik.club", HttpStatusCode.NotFound)]
    [InlineData(true, "statik.club", "127.0.0.1", "https://statik.club", HttpStatusCode.NotFound)]
    [InlineData(true, "app.statik.club", "192.0.2.1", "https://app.statik.club", HttpStatusCode.NotFound)]
    [InlineData(true, "app.statik.club", "127.0.0.1", "https://evil.example", HttpStatusCode.Forbidden)]
    public async Task DevelopmentAdminRejectsOtherEnvironmentsHostsConnectionsAndOrigins(bool development, string host,
        string remote, string origin, HttpStatusCode expected)
    {
        using var server = CreateServer(development);
        using var client = CreateClient(server);
        client.BaseAddress = new Uri($"https://{host}");
        client.DefaultRequestHeaders.Remove("Origin");
        client.DefaultRequestHeaders.Add("Origin", origin);
        client.DefaultRequestHeaders.Add("X-Test-Remote", remote);
        Assert.Equal(expected, (await client.PostAsJsonAsync("/api/admin/users", new { })).StatusCode);
        Assert.Equal(expected, (await client.PostAsync("/admin/CoreAdminData/DeleteEntityPost", null)).StatusCode);
    }

    private static Dictionary<string, string> EmailParameters(string message) => QueryHelpers.ParseQuery(
            new Uri(message.Split('\n').Single(line => line.StartsWith("https://"))).Fragment.Split('?')[1])
        .ToDictionary(pair => pair.Key, pair => pair.Value.ToString());

    [Fact]
    public async Task FailedEmailDeliveryCanBeRetriedWithoutSigningIn()
    {
        using var server = CreateServer();
        using var client = CreateClient(server);
        await Csrf(client);
        var emails = (TestEmailSender)server.Services.GetRequiredService<IAccountEmailSender>();
        emails.Fail = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync("/api/auth/signup",
            new { username = "alice", displayName = "Alice", email = "alice@example.test", password = "Password!123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
        emails.Fail = false;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/resend-confirmation", new { email = "alice@example.test" })).StatusCode);
        Assert.Single(emails.Messages);
    }

    private static readonly SiteOrigins Sites = new() { Public = new Uri("https://statik.club"), App = new Uri("https://app.statik.club") };

    private static HttpClient CreateClient(IHost server)
    {
        var client = server.GetTestClient();
        client.BaseAddress = Sites.App;
        client.DefaultRequestHeaders.Add("Origin", Sites.App.GetLeftPart(UriPartial.Authority));
        return client;
    }

    private static IHost CreateServer(bool development = false)
    {
        var databaseName = Guid.NewGuid().ToString();
        return new HostBuilder().ConfigureWebHost(web => web.UseEnvironment(development ? "Development" : "Production").UseTestServer()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddLogging();
                services.AddDbContext<StatikDbContext>(options => options.UseInMemoryDatabase(databaseName));
                services.AddManagementAuthentication();
                services.AddSingleton(Sites);
                services.AddAuthenticationRateLimits();
                services.AddSingleton<IAccountEmailSender, TestEmailSender>();
                services.AddScoped<AccountEmail>();
                services.AddSingleton<UserPageRenderer>();
            })
            .Configure(app =>
            {
                app.Use(async (context, next) =>
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers["X-Test-Remote"].FirstOrDefault() ?? "127.0.0.1");
                    await next(context);
                });
                app.UseDevelopmentAdminGuard(Sites, development);
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseRateLimiter();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapManagementEndpoints(Sites);
                    endpoints.MapUserPageEndpoints(Sites);
                });
            })).Start();
    }

    private static async Task SignUp(IHost server, HttpClient client, string username)
    {
        await Csrf(client);
        var response = await client.PostAsJsonAsync("/api/auth/signup",
            new { username, displayName = username, email = $"{username}@example.test", password = "Password!123" });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
        var email = (TestEmailSender)server.Services.GetRequiredService<IAccountEmailSender>();
        var parameters = QueryHelpers.ParseQuery(new Uri(email.Messages.Last().Split('\n').Single(line => line.StartsWith("https://"))).Fragment.Split('?')[1]);
        response = await client.PostAsJsonAsync("/api/auth/confirm-email", new { email = parameters["email"].ToString(), token = parameters["token"].ToString() });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        response = await client.PostAsJsonAsync("/api/auth/login", new { email = $"{username}@example.test", password = "Password!123" });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        CaptureCookies(client, response);
    }

    public class TestEmailSender : IAccountEmailSender
    {
        public bool Fail { get; set; }
        public List<string> Messages { get; } = [];
        public Task SendAsync(string email, string subject, string text, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new IOException("Test mail server is unavailable.");
            Messages.Add(text);
            return Task.CompletedTask;
        }
    }

    private static async Task Csrf(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        CaptureCookies(client, response);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", body.GetProperty("token").GetString());
    }

    private static void CaptureCookies(HttpClient client, HttpResponseMessage response)
    {
        var cookies = new CookieContainer();
        var address = client.BaseAddress!;
        if (client.DefaultRequestHeaders.TryGetValues("Cookie", out var existing))
        {
            foreach (var cookie in string.Join("; ", existing).Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                cookies.SetCookies(address, cookie);
            }
        }
        if (response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            foreach (var value in values)
            {
                cookies.SetCookies(address, value);
            }
        }
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookies.GetCookieHeader(address));
    }
}
