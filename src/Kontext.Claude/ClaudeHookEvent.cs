using Kontext.Domain;

namespace Kontext.Claude;

internal sealed record ClaudeHookEvent(TerminalSessionId Session, string Name, string? NotificationType = null);

internal sealed class ClaudeHookEventArgs(ClaudeHookEvent hook) : EventArgs
{
    public ClaudeHookEvent Hook { get; } = hook;
}
