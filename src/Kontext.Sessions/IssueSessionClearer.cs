using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Sessions;

internal sealed class IssueSessionClearer(
    IIssueWorkTracker tracker,
    ISessionStore sessions,
    ITerminalSessionService terminals,
    IIssueSessionRegistry registry,
    IWorkspaceRemover workspaces,
    TimeProvider time) : IIssueSessionClearer
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(5);

    public async Task ClearAsync(GitHubIssue issue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);

        var key = issue.Key;
        var state = tracker.StateOf(key);
        var stored = sessions.Find(key);
        var worktreePath = state.WorktreePath ?? stored?.WorktreePath;
        var branchName = state.BranchName ?? stored?.BranchName;

        await EndSessionAsync(key);

        if (worktreePath is not null && branchName is not null)
        {
            try
            {
                await workspaces.RemoveAsync(issue.Repository, worktreePath, branchName, cancellationToken);
            }
            catch (KontextException ex)
            {
                tracker.Report(key, new IssueWorkState(IssueWorkStatus.Failed, branchName, worktreePath, ex.Message, SessionEnded: true));
                return;
            }
        }

        sessions.Remove(key);
        tracker.Report(key, IssueWorkState.NotStarted);
    }

    private async Task EndSessionAsync(IssueKey key)
    {
        if (registry.Find(key) is not { } session)
        {
            return;
        }

        terminals.Close(session.Id);

        try
        {
            await session.Exited.WaitAsync(ExitTimeout, time);
        }
        catch (TimeoutException)
        {
        }

        registry.Remove(key);
    }
}
