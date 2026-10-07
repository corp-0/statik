using Microsoft.AspNetCore.Identity;
using Statik.Server.Models;

namespace Statik.Server.Commands;

public static class ConfirmEmailCommand
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        var manager = services.GetRequiredService<UserManager<User>>();
        var user = await ConsoleUsers.FindAsync(manager, args);
        if (user is null)
        {
            return 1;
        }

        if (user.EmailConfirmed)
        {
            Console.WriteLine($"Email for '{user.UserName}' is already confirmed.");
            return 0;
        }

        var result = await manager.ConfirmEmailAsync(user, await manager.GenerateEmailConfirmationTokenAsync(user));
        if (result.Succeeded)
        {
            Console.WriteLine($"Confirmed {user.Email} for '{user.UserName}'.");
        }

        return await ConsoleUsers.ReportAsync(result);
    }
}
