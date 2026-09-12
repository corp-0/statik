using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Statik.Server.Data;
using Statik.Server.Identity;
using Statik.Server.Models;

namespace Statik.Server.Tests;

public class UserValidationTests
{
    [Theory]
    [InlineData(null, "InvalidUserNameLength")]
    [InlineData("", "InvalidUserNameLength")]
    [InlineData("abcdefghijklmnopqrstuvwxyz", "InvalidUserNameLength")]
    [InlineData("gilles smith", "InvalidUserName")]
    [InlineData("gilles/smith", "InvalidUserName")]
    [InlineData("gilles?x", "InvalidUserName")]
    [InlineData("gilles#x", "InvalidUserName")]
    [InlineData("gilles%2fsmith", "InvalidUserName")]
    [InlineData("gilles\\smith", "InvalidUserName")]
    [InlineData("gilles.smith", "InvalidUserName")]
    [InlineData("gilles@smith", "InvalidUserName")]
    [InlineData("gilles+smith", "InvalidUserName")]
    [InlineData("gillé", "InvalidUserName")]
    [InlineData("about", "ReservedUserName")]
    [InlineData("privacy", "ReservedUserName")]
    [InlineData("terms", "ReservedUserName")]
    [InlineData("api", "ReservedUserName")]
    [InlineData("assets", "ReservedUserName")]
    [InlineData("health", "ReservedUserName")]
    [InlineData("API", "ReservedUserName")]
    [InlineData("HeAlTh", "ReservedUserName")]
    public async Task RejectsInvalidNamesOnCreationAndUpdate(string? username, string errorCode)
    {
        using var services = CreateServices();
        using var scope = services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var db = scope.ServiceProvider.GetRequiredService<StatikDbContext>();

        var creation = await manager.CreateAsync(new User { UserName = username, DisplayName = "Gilles" });
        Assert.Contains(creation.Errors, error => error.Code == errorCode);
        Assert.False(await db.Users.AnyAsync());

        var user = new User { UserName = "alice", DisplayName = "Gilles" };
        Assert.True((await manager.CreateAsync(user)).Succeeded);

        user.UserName = username;
        var update = await manager.UpdateAsync(user);
        Assert.Contains(update.Errors, error => error.Code == errorCode);

        db.ChangeTracker.Clear();
        user = await db.Users.SingleAsync();
        Assert.Equal("alice", user.UserName);

        var rename = await manager.SetUserNameAsync(user, username);
        Assert.Contains(rename.Errors, error => error.Code == errorCode);

        db.ChangeTracker.Clear();
        Assert.Equal("alice", (await db.Users.SingleAsync()).UserName);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("abcdefghijklmnopqrstuvwxy")]
    [InlineData("Gilles_42-test")]
    public async Task AcceptsValidNamesOnCreationAndRename(string username)
    {
        using var services = CreateServices();
        using var scope = services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User { UserName = username, DisplayName = "Gilles" };

        Assert.True((await manager.CreateAsync(user)).Succeeded);
        Assert.True((await manager.SetUserNameAsync(user, "another-user")).Succeeded);
        Assert.True((await manager.SetUserNameAsync(user, username)).Succeeded);
    }

    [Fact]
    public async Task PreservesCaseInsensitiveUniquenessValidation()
    {
        using var services = CreateServices();
        using var scope = services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        Assert.True((await manager.CreateAsync(new User { UserName = "alice", DisplayName = "Gilles" })).Succeeded);
        var result = await manager.CreateAsync(new User { UserName = "ALICE", DisplayName = "Someone else" });

        Assert.Contains(result.Errors, error => error.Code == "DuplicateUserName");
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<StatikDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<User>(options =>
            {
                options.User.AllowedUserNameCharacters = StatikUserValidator.AllowedUserNameCharacters;
            })
            .AddEntityFrameworkStores<StatikDbContext>()
            .AddUserValidator<StatikUserValidator>();
        return services.BuildServiceProvider();
    }
}
