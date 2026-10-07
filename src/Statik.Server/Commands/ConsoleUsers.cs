using Microsoft.AspNetCore.Identity;
using Statik.Server.Models;

namespace Statik.Server.Commands;

public static class ConsoleUsers
{
    public static async Task<User?> FindAsync(UserManager<User> manager, string[] args)
    {
        if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            await Console.Error.WriteLineAsync(ConsoleCommands.Usage(args[0]));
            return null;
        }

        var user = await manager.FindByNameAsync(args[1].Trim());
        if (user is null)
        {
            await Console.Error.WriteLineAsync($"User '{args[1].Trim()}' does not exist.");
        }

        return user;
    }

    public static async Task<int> ReportAsync(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            await Console.Error.WriteLineAsync(error.Description);
        }

        return result.Succeeded ? 0 : 1;
    }
}
