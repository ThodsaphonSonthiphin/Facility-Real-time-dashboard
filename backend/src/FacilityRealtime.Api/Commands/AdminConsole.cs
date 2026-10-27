using System.Text;

namespace FacilityRealtime.Api.Commands;

/// <summary>What reset-admin asks and says; tests answer it without a terminal.</summary>
public interface IAdminConsole
{
    string? ReadLine(string prompt);

    /// <summary>Typing is not shown on the screen (facility-0092 decision 5).</summary>
    string? ReadSecret(string prompt);

    void WriteLine(string text);
}

/// <summary>The server's terminal. Piped input (a script) is read as lines; nothing is echoed either way.</summary>
public sealed class SystemAdminConsole : IAdminConsole
{
    public string? ReadLine(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine();
    }

    public string? ReadSecret(string prompt)
    {
        Console.Write(prompt);
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine();
        }

        var typed = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return typed.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (typed.Length > 0)
                {
                    typed.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                typed.Append(key.KeyChar);
            }
        }
    }

    public void WriteLine(string text) => Console.WriteLine(text);
}
