using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Statik.Server.Commands;
using Statik.Server.Data;
using Statik.Server.Identity;
using Statik.Server.Models;

namespace Statik.Server.Tests;

// Commands use the process-wide Console, so no parallel runs.
public class ConsoleCommandTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private readonly TextWriter _originalOut = Console.Out;
    private readonly TextWriter _originalError = Console.Error;
    private readonly TextReader _originalIn = Console.In;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        var databaseName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<StatikDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddManagementAuthentication();
        _app = builder.Build();

        using var scope = _app.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var alice = new User { UserName = "alice", DisplayName = "Alice", Email = "alice@example.test" };
        Assert.True((await manager.CreateAsync(alice, "Password!123")).Succeeded);
        var db = scope.ServiceProvider.GetRequiredService<StatikDbContext>();
        db.UserPages.Add(new UserPage { UserId = alice.Id, Owner = alice, BodyTemplate = "Hi", Css = "" });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        Console.SetIn(_originalIn);
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task ListUsersShowsAccountState()
    {
        var (code, output, _) = await Run("", "list-users");

        Assert.Equal(0, code);
        Assert.Matches(@"alice\s+alice@example\.test\s+no\s+yes\s+no", output);
        Assert.Contains("1 user(s).", output);
    }

    [Fact]
    public async Task DeleteUserRequiresTypedConfirmation()
    {
        var (code, _, error) = await Run("bob\n", "delete-user", "alice");
        Assert.Equal(1, code);
        Assert.Contains("nothing was deleted", error);
        Assert.NotNull(await FindUser("alice"));

        (code, _, _) = await Run("alice\n", "delete-user", "alice");
        Assert.Equal(0, code);
        Assert.Null(await FindUser("alice"));
        using var scope = _app.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<StatikDbContext>().UserPages);
    }

    [Fact]
    public async Task ConfirmEmailAllowsLogin()
    {
        Assert.Equal(0, (await Run("", "confirm-email", "alice")).Code);

        Assert.True((await FindUser("alice"))!.EmailConfirmed);
    }

    [Fact]
    public async Task SetPasswordReplacesPasswordAndClearsLockout()
    {
        using (var scope = _app.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            await manager.SetLockoutEndDateAsync((await manager.FindByNameAsync("alice"))!, DateTimeOffset.UtcNow.AddHours(1));
        }

        var (code, _, error) = await Run("short\nshort\n", "set-password", "alice");
        Assert.Equal(1, code);
        Assert.NotEmpty(error);

        Assert.Equal(0, (await Run("NewPassword!456\nNewPassword!456\n", "set-password", "alice")).Code);
        using (var scope = _app.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var alice = (await manager.FindByNameAsync("alice"))!;
            Assert.True(await manager.CheckPasswordAsync(alice, "NewPassword!456"));
            Assert.False(await manager.IsLockedOutAsync(alice));
        }
    }

    [Fact]
    public async Task CreateUserConfirmsEmailAndRejectsInvalidAddresses()
    {
        Assert.Equal(1, (await Run("Bob\nnot-an-email\n", "create-user", "bob")).Code);
        Assert.Null(await FindUser("bob"));

        Assert.Equal(0, (await Run("Bob\nbob@example.test\n\nPassword!123\nPassword!123\n", "create-user", "bob")).Code);
        Assert.True((await FindUser("bob"))!.EmailConfirmed);
    }

    [Theory]
    [InlineData("delete-user")]
    [InlineData("delete-user", "nobody")]
    [InlineData("set-password", "a", "b")]
    public async Task UserCommandsRejectMissingUsersAndBadArguments(params string[] args)
    {
        Assert.Equal(1, (await Run("", args)).Code);
    }

    [Fact]
    public async Task HelpListsCommands()
    {
        Assert.True(ConsoleCommands.IsCommand(["help"]));
        Assert.False(ConsoleCommands.IsCommand([]));
        var (code, output, _) = await Run("", "help");

        Assert.Equal(0, code);
        Assert.Contains("statik delete-user <username>", output);
    }

    private async Task<User?> FindUser(string username)
    {
        using var scope = _app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<User>>().FindByNameAsync(username);
    }

    private async Task<(int Code, string Output, string Error)> Run(string input, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        Console.SetIn(new StringReader(input));
        Console.SetOut(output);
        Console.SetError(error);
        var code = await ConsoleCommands.RunAsync(_app.Services, args);
        return (code, output.ToString(), error.ToString());
    }
}
