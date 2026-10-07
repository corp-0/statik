using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Statik.Server.Data;
using Statik.Server.Models;

namespace Statik.Server.Commands;

public static class DeleteUserCommand
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        var manager = services.GetRequiredService<UserManager<User>>();
        var db = services.GetRequiredService<StatikDbContext>();
        var user = await ConsoleUsers.FindAsync(manager, args);
        if (user is null)
        {
            return 1;
        }

        var page = await db.UserPages.FirstOrDefaultAsync(page => page.UserId == user.Id);
        Console.WriteLine($"User: {user.UserName} <{user.Email ?? "no email"}>");
        Console.WriteLine(page is null ? "Page: none" : "Page: yes, it will be deleted too");
        if (ConsolePrompts.Read("Type the username to delete this account").Trim() != user.UserName)
        {
            await Console.Error.WriteLineAsync("Username did not match, nothing was deleted.");
            return 1;
        }

        // Track the page so EF cascades the delete on providers without foreign keys.
        var result = await manager.DeleteAsync(user);
        if (result.Succeeded)
        {
            Console.WriteLine($"Deleted user '{user.UserName}'.");
        }

        return await ConsoleUsers.ReportAsync(result);
    }
}
