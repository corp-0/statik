using System.Data.Common;

namespace Statik.Server.Commands;

public static class ConsoleCommands
{
    private static readonly Dictionary<string, (string Usage, Func<IServiceProvider, string[], Task<int>> Run)> Commands = new()
    {
        ["migrate"] = ("migrate", MigrateCommand.RunAsync),
        ["list-users"] = ("list-users", ListUsersCommand.RunAsync),
        ["create-user"] = ("create-user [username]", CreateUserCommand.RunAsync),
        ["delete-user"] = ("delete-user <username>", DeleteUserCommand.RunAsync),
        ["confirm-email"] = ("confirm-email <username>", ConfirmEmailCommand.RunAsync),
        ["set-password"] = ("set-password <username>", SetPasswordCommand.RunAsync),
        ["create-page"] = ("create-page [username]", CreatePageCommand.RunAsync),
    };

    public static bool IsCommand(string[] args) => args.FirstOrDefault() is { } name && (name == "help" || Commands.ContainsKey(name));

    public static string Usage(string command) => $"Usage: statik {Commands[command].Usage}";

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        if (!Commands.TryGetValue(args[0], out var command))
        {
            Console.WriteLine("Commands:");
            foreach (var (usage, _) in Commands.Values)
            {
                Console.WriteLine($"  statik {usage}");
            }

            return 0;
        }

        try
        {
            using var scope = services.CreateScope();
            return await command.Run(scope.ServiceProvider, args);
        }
        catch (OperationCanceledException error)
        {
            await Console.Error.WriteLineAsync(error.Message);
            return 1;
        }
        catch (Exception error) when (error is DbException || error.InnerException is DbException)
        {
            await Console.Error.WriteLineAsync($"Database error: {(error as DbException ?? error.InnerException)!.Message}");
            return 1;
        }
    }
}
