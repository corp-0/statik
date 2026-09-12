namespace Statik.Server.Commands;

public static class ConsoleCommands
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        try
        {
            using var scope = services.CreateScope();
            return args[0] switch
            {
                "create-user" => await CreateUserCommand.RunAsync(scope.ServiceProvider, args),
                "create-page" => await CreatePageCommand.RunAsync(scope.ServiceProvider, args),
                "migrate" => await MigrateCommand.RunAsync(scope.ServiceProvider, args),
                _ => throw new ArgumentException("Unknown management command.")
            };
        }
        catch (OperationCanceledException error)
        {
            await Console.Error.WriteLineAsync(error.Message);
            return 1;
        }
    }
}
