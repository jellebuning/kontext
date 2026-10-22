using Kontext.Contracts;
using Kontext.Kernel;

namespace Kontext.Claude;

internal sealed class ClaudeActivityReporter(IIssueSessionRegistry registry, IIssueWorkTracker tracker, IUiDispatcher ui)
{
    public void OnHook(ClaudeHookEvent hook)
    {
        ArgumentNullException.ThrowIfNull(hook);

        _ = ui.RunAsync(() => Apply(hook));
    }

    private void Apply(ClaudeHookEvent hook)
    {
        if (registry.IssueOf(hook.Session) is not { } issue)
        {
            return;
        }

        var state = tracker.StateOf(issue);

        if (state.SessionEnded)
        {
            return;
        }

        if (ClaudeActivityMapper.Next(state.Status, hook) is { } next && next != state.Status)
        {
            tracker.Report(issue, state with { Status = next });
        }
    }
}
