using Kontext.Domain;

namespace Kontext.Claude;

internal sealed class ClaudeLaunchBuilder(ClaudeOptions options)
{
    public TerminalSessionOptions BuildNew(TerminalSessionId sessionId, string worktreePath, string? settings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worktreePath);

        return new TerminalSessionOptions(worktreePath, options.Command, Arguments("--session-id", sessionId, settings));
    }

    public TerminalSessionOptions BuildResume(TerminalSessionId sessionId, string worktreePath, string? settings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worktreePath);

        return new TerminalSessionOptions(worktreePath, options.Command, Arguments("--resume", sessionId, settings));
    }

    private List<string> Arguments(string sessionFlag, TerminalSessionId sessionId, string? settings)
    {
        List<string> arguments = [sessionFlag, Format(sessionId), "--permission-mode", options.PermissionMode];

        if (settings is not null)
        {
            arguments.AddRange(["--settings", settings]);
        }

        return arguments;
    }

    private static string Format(TerminalSessionId id) => id.Value.ToString("D", System.Globalization.CultureInfo.InvariantCulture);
}
