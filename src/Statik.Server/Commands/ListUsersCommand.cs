using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Statik.Server.Data;
using Statik.Server.Models;

namespace Statik.Server.Commands;

public static class ListUsersCommand
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        if (args.Length != 1)
        {
            await Console.Error.WriteLineAsync(ConsoleCommands.Usage("list-users"));
            return 1;
        }

        var manager = services.GetRequiredService<UserManager<User>>();
        var db = services.GetRequiredService<StatikDbContext>();
        var users = await manager.Users.OrderBy(user => user.UserName)
            .Select(user => new
            {
                user.UserName,
                user.Email,
                user.EmailConfirmed,
                HasPage = db.UserPages.Any(page => page.UserId == user.Id),
                Locked = user.LockoutEnd > DateTimeOffset.UtcNow
            })
            .ToListAsync();

        var rows = users.Select(user => new[]
        {
            user.UserName ?? "", user.Email ?? "(none)", user.EmailConfirmed ? "yes" : "no",
            user.HasPage ? "yes" : "no", user.Locked ? "yes" : "no"
        }).Prepend(["USERNAME", "EMAIL", "CONFIRMED", "PAGE", "LOCKED"]).ToList();
        var widths = Enumerable.Range(0, rows[0].Length).Select(column => rows.Max(row => row[column].Length)).ToArray();
        foreach (var row in rows)
        {
            Console.WriteLine(string.Join("  ", row.Select((cell, column) => cell.PadRight(widths[column]))).TrimEnd());
        }

        Console.WriteLine($"{users.Count} user(s).");
        return 0;
    }
}
