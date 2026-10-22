using Kontext.Domain;

namespace Kontext.Claude;

internal static class ClaudeActivityMapper
{
    public static IssueWorkStatus? Next(IssueWorkStatus current, ClaudeHookEvent hook)
    {
        ArgumentNullException.ThrowIfNull(hook);

        if (current is IssueWorkStatus.NotStarted or IssueWorkStatus.Preparing or IssueWorkStatus.Failed)
        {
            return null;
        }

        return hook.Name switch
        {
            "UserPromptSubmit" or "PreToolUse" or "PostToolUse" or "PostToolUseFailure" => IssueWorkStatus.Running,

            "PermissionRequest" or "StopFailure" or "Stop" => IssueWorkStatus.NeedsInput,
            "Notification" when IsWaitingForUser(hook.NotificationType) => IssueWorkStatus.NeedsInput,
            "Notification" when hook.NotificationType == "idle_prompt" && current == IssueWorkStatus.Running => IssueWorkStatus.NeedsInput,

            _ => null,
        };
    }

    private static bool IsWaitingForUser(string? notificationType)
        => notificationType is "permission_prompt" or "elicitation_dialog" or "elicitation_url_dialog" or "agent_needs_input";
}
