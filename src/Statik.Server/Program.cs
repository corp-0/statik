using Microsoft.EntityFrameworkCore;
using Statik.Server.Commands;
using Statik.Server.Data;
using Statik.Server.Endpoints;
using Statik.Server.Identity;
using Statik.Server.Rendering;
using Statik.Server.Hosting;
using Microsoft.AspNetCore.DataProtection;

var isManagementCommand = ConsoleCommands.IsCommand(args);

var builder = WebApplication.CreateBuilder(isManagementCommand ? [] : args);
if (isManagementCommand)
{
    // Commands print their own database errors, EF logs would bury them.
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
}

builder.Services.AddDbContext<StatikDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Statik")
        ?? throw new InvalidOperationException("Connection string 'Statik' is missing.")));

var sites = SiteOrigins.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(sites);
builder.Services.AddManagementAuthentication();
builder.Services.AddAuthenticationRateLimits();
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IAccountEmailSender, SmtpAccountEmailSender>();
builder.Services.AddScoped<AccountEmail>();
builder.Services.AddProxyHeaders(builder.Configuration);
// Keys must survive container replacement or cookies and email tokens break.
builder.Services.AddDataProtection()
    .SetApplicationName("Statik")
    .PersistKeysToDbContext<StatikDbContext>();

builder.Services.AddSingleton<UserPageRenderer>();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDevelopmentAdmin();
}

var app = builder.Build();

if (isManagementCommand)
{
    Environment.ExitCode = await ConsoleCommands.RunAsync(app.Services, args);
    await app.DisposeAsync();
    return;
}

// Watchtower runs no migration step, so migrate on startup.
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<StatikDbContext>().Database.MigrateAsync();
}

app.UseForwardedHeaders();
app.UseDevelopmentAdminGuard(sites, app.Environment.IsDevelopment());
app.Use(async (context, next) =>
{
    if (!string.Equals(context.Request.Host.Host, sites.Public.Host, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(context.Request.Host.Host, sites.App.Host, StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next(context);
});
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapManagementEndpoints(sites);
app.MapUserPageEndpoints(sites);
if (app.Environment.IsDevelopment())
{
    app.MapControllerRoute("development-admin", "admin/{controller=CoreAdmin}/{action=Index}/{id?}")
        .RequireHost(sites.App.Host);
}

app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html")
    .RequireHost(sites.App.Host);
app.MapFallbackToFile("/", "index.html")
    .RequireHost(sites.Public.Host);

app.Run();
