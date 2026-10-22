namespace Kontext.Claude;

public sealed class ClaudeEnvironmentScrubber(IEnvironmentVariables environment)
{
    private const string ParentSessionMarker = "CLAUDECODE";
    private const string NoColor = "NO_COLOR";

    private static readonly string[] ParentSessionVariables =
    [
        ParentSessionMarker,
        "CLAUDE_PID",
        "CLAUDE_CODE_CHILD_SESSION",
        "CLAUDE_CODE_ENTRYPOINT",
        "CLAUDE_CODE_MESSAGING_SOCKET",
        "CLAUDE_CODE_MESSAGING_TOKEN",
        "CLAUDE_CODE_SESSION_ATTENDED",
        "CLAUDE_CODE_SESSION_ID",
    ];

    public static IReadOnlyList<string> VariableNames => ParentSessionVariables;

    public IReadOnlyList<string> Scrub()
    {
        var launchedFromClaude = environment.IsSet(ParentSessionMarker);
        var removed = new List<string>();

        foreach (var name in ParentSessionVariables)
        {
            if (environment.IsSet(name))
            {
                environment.Remove(name);
                removed.Add(name);
            }
        }

        if (launchedFromClaude && environment.IsSet(NoColor))
        {
            environment.Remove(NoColor);
            removed.Add(NoColor);
        }

        return removed;
    }
}
