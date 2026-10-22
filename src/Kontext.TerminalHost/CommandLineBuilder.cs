using System.Text;

namespace Kontext.TerminalHost;

public static class CommandLineBuilder
{
    public static string Build(string command, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(arguments);

        var builder = new StringBuilder(Quote(command));
        foreach (var argument in arguments)
        {
            builder.Append(' ').Append(Quote(argument));
        }

        return builder.ToString();
    }

    private static string Quote(string argument)
    {
        if (argument.Length > 0 && argument.AsSpan().IndexOfAny(" \t\n\v\"") < 0)
        {
            return argument;
        }

        var builder = new StringBuilder("\"");
        var backslashes = 0;

        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashes++;
            }
            else if (character == '"')
            {
                builder.Append('\\', (backslashes * 2) + 1).Append('"');
                backslashes = 0;
            }
            else
            {
                builder.Append('\\', backslashes).Append(character);
                backslashes = 0;
            }
        }

        builder.Append('\\', backslashes * 2).Append('"');
        return builder.ToString();
    }
}
