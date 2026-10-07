using Microsoft.AspNetCore.Identity;
using Statik.Server.Models;

namespace Statik.Server.Commands;

public static class SetPasswordCommand
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        var manager = services.GetRequiredService<UserManager<User>>();
        var user = await ConsoleUsers.FindAsync(manager, args);
        if (user is null)
        {
            return 1;
        }

        var password = ConsolePrompts.ReadNewPassword();
        // Resetting also signs the user out everywhere.
        var result = await manager.ResetPasswordAsync(user, await manager.GeneratePasswordResetTokenAsync(user), password);
        if (!result.Succeeded)
        {
            return await ConsoleUsers.ReportAsync(result);
        }

        await manager.SetLockoutEndDateAsync(user, null);
        await manager.ResetAccessFailedCountAsync(user);
        Console.WriteLine($"Set a new password for '{user.UserName}' and cleared any lockout.");
        return 0;
    }
}
