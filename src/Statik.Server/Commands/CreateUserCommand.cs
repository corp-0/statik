using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Statik.Server.Models;

namespace Statik.Server.Commands;

public static class CreateUserCommand
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        if (args.Length > 2)
        {
            await Console.Error.WriteLineAsync(ConsoleCommands.Usage("create-user"));
            return 1;
        }

        var username = args.Length == 2 && !string.IsNullOrWhiteSpace(args[1])
            ? args[1].Trim()
            : ConsolePrompts.ReadRequired("Username");
        var userManager = services.GetRequiredService<UserManager<User>>();
        if (await userManager.FindByNameAsync(username) is not null)
        {
            await Console.Error.WriteLineAsync($"User '{username}' already exists.");
            return 1;
        }

        var displayName = ConsolePrompts.ReadRequired("Display name");
        var email = ConsolePrompts.ReadRequired("Email");
        if (!new EmailAddressAttribute().IsValid(email))
        {
            await Console.Error.WriteLineAsync($"'{email}' is not a valid email address.");
            return 1;
        }

        var user = new User
        {
            UserName = username,
            Email = email,
            // Created by an operator, so the email counts as confirmed.
            EmailConfirmed = true,
            DisplayName = displayName,
            About = ConsolePrompts.Read("About (optional)")
        };

        var validationErrors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(user, new ValidationContext(user), validationErrors, true))
        {
            foreach (var error in validationErrors)
            {
                await Console.Error.WriteLineAsync(error.ErrorMessage);
            }

            return 1;
        }

        var result = await userManager.CreateAsync(user, ConsolePrompts.ReadNewPassword());
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                await Console.Error.WriteLineAsync(error.Description);
            }

            return 1;
        }

        Console.WriteLine($"Created user '{user.UserName}'.");
        return 0;
    }
}
