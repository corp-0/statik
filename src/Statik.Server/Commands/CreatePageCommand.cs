using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Statik.Server.Data;
using Statik.Server.Models;

namespace Statik.Server.Commands;

public static class CreatePageCommand
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        if (args.Length > 2)
        {
            await Console.Error.WriteLineAsync("Usage: dotnet run --project src/Statik.Server -- create-page [username]");
            return 1;
        }

        var username = args.Length == 2 && !string.IsNullOrWhiteSpace(args[1])
            ? args[1].Trim()
            : ConsolePrompts.ReadRequired("Owner username");
        var manager = services.GetRequiredService<UserManager<User>>();
        var db = services.GetRequiredService<StatikDbContext>();
        var user = await manager.FindByNameAsync(username);
        if (user is null)
        {
            await Console.Error.WriteLineAsync($"User '{username}' does not exist.");
            return 1;
        }

        if (await db.UserPages.AnyAsync(page => page.UserId == user.Id))
        {
            await Console.Error.WriteLineAsync($"User '{username}' already has a page.");
            return 1;
        }

        var page = new UserPage
        {
            UserId = user.Id,
            Owner = user,
            BodyTemplate = ConsolePrompts.ReadFile("HTML template file (Enter for <main></main>)", "<main></main>"),
            Css = ConsolePrompts.ReadFile("CSS file (Enter for no styles)", "")
        };

        db.UserPages.Add(page);
        await db.SaveChangesAsync();

        Console.WriteLine($"Created page for '{user.UserName}'.");
        return 0;
    }
}
