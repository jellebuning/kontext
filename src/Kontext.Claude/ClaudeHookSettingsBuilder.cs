using System.Globalization;
using System.Text.Json;
using Kontext.Domain;

namespace Kontext.Claude;

internal static class ClaudeHookSettingsBuilder
{
    private static readonly string[] Events =
    [
        "UserPromptSubmit",
        "PreToolUse",
        "PostToolUse",
        "PostToolUseFailure",
        "PermissionRequest",
        "Notification",
        "Stop",
        "StopFailure",
    ];

    private const int TimeoutSeconds = 5;

    public static string Build(Uri address, TerminalSessionId session)
    {
        ArgumentNullException.ThrowIfNull(address);

        var url = new Uri(address, $"hook/{session.Value.ToString("D", CultureInfo.InvariantCulture)}").AbsoluteUri;

        var hooks = Events.ToDictionary(
            name => name,
            _ => new object[] { new { hooks = new object[] { new { type = "http", url, timeout = TimeoutSeconds } } } });

        return JsonSerializer.Serialize(new { hooks });
    }
}
