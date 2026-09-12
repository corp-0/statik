using System.Text;

namespace Statik.Server.Commands;

public static class ConsolePrompts
{
    public static string Read(string label)
    {
        Console.Write($"{label}: ");
        return Console.ReadLine() ?? throw new OperationCanceledException("Input ended; nothing was saved.");
    }

    public static string ReadRequired(string label)
    {
        while (true)
        {
            var value = Read(label).Trim();
            if (value.Length > 0)
            {
                return value;
            }

            Console.Error.WriteLine($"{label} is required.");
        }
    }

    public static string ReadPassword(string label)
    {
        if (Console.IsInputRedirected)
        {
            return Read(label);
        }

        Console.Write($"{label}: ");
        var password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return password.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                }
            }
            else if (key.Key == ConsoleKey.Escape || key.KeyChar == '\u0004')
            {
                throw new OperationCanceledException("Cancelled; nothing was saved.");
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }
    }

    public static string ReadFile(string label, string defaultContent)
    {
        while (true)
        {
            var path = Read(label).Trim();
            if (path.Length == 0)
            {
                return defaultContent;
            }

            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                Console.Error.WriteLine($"Could not read file: {error.Message}");
            }
        }
    }
}
