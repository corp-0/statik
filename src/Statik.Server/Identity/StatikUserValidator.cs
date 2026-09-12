using Microsoft.AspNetCore.Identity;
using Statik.Server.Models;

namespace Statik.Server.Identity;

public class StatikUserValidator : IUserValidator<User>
{
    public const string AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_";

    private static readonly HashSet<string> ReservedUserNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "about", "privacy", "terms", "api", "assets", "health", "admin", "staff", "gilles"
    };

    public async Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user)
    {
        var username = await manager.GetUserNameAsync(user);
        if (username is null || username.Length is < 1 or > 25)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "InvalidUserNameLength",
                Description = "Username must be between 1 and 25 characters."
            });
        }

        if (ReservedUserNames.Contains(username))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "ReservedUserName",
                Description = "This username is reserved."
            });
        }

        return IdentityResult.Success;
    }
}
