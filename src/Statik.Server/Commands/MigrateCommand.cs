using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Statik.Server.Data;

namespace Statik.Server.Commands;

public static class MigrateCommand
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        if (args.Length != 1)
        {
            await Console.Error.WriteLineAsync("Usage: dotnet run --project src/Statik.Server -- migrate");
            return 1;
        }

        var db = services.GetRequiredService<StatikDbContext>();
        try
        {
            await db.Database.MigrateAsync();
        }
        catch (Exception error) when (error is DbException or InvalidOperationException)
        {
            await Console.Error.WriteLineAsync($"Database migration failed: {error.Message}");
            return 1;
        }

        Console.WriteLine("Database migrations are up to date.");
        return 0;
    }
}
