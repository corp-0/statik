using Microsoft.EntityFrameworkCore;
using Statik.Server.Commands;
using Statik.Server.Data;
using Statik.Server.Endpoints;
using Statik.Server.Identity;
using Statik.Server.Models;
using Statik.Server.Rendering;

var isManagementCommand = args.FirstOrDefault() is "create-user" or "create-page" or "migrate";

var builder = WebApplication.CreateBuilder(isManagementCommand ? [] : args);

builder.Services.AddDbContext<StatikDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Statik")
        ?? throw new InvalidOperationException("Connection string 'Statik' is missing.")));

builder.Services.AddIdentityCore<User>(options =>
    {
        options.User.AllowedUserNameCharacters = StatikUserValidator.AllowedUserNameCharacters;
    })
    .AddEntityFrameworkStores<StatikDbContext>()
    .AddUserValidator<StatikUserValidator>();

builder.Services.AddSingleton<UserPageRenderer>();

var app = builder.Build();

if (isManagementCommand)
{
    Environment.ExitCode = await ConsoleCommands.RunAsync(app.Services, args);
    await app.DisposeAsync();
    return;
}

app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapUserPageEndpoints();

app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html")
    .RequireHost("app.statik.club", "localhost", "127.0.0.1");

app.Run();
