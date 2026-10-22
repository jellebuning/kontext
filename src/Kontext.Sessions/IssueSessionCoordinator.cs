using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Sessions;

internal sealed class IssueSessionCoordinator(
    IWorkspaceProvisioner provisioner,
    IIssueWorkTracker tracker,
    ISessionStore sessions,
    ITerminalSessionService terminals,
    IIssueSessionRegistry registry,
    IClaudeLauncher claude,
    IUiDispatcher ui) : IIssueSessionCoordinator
{
    public async Task StartOrResumeAsync(GitHubIssue issue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);

        var key = issue.Key;
        var before = tracker.StateOf(key);

        if (before.Status is IssueWorkStatus.Preparing or IssueWorkStatus.Running or IssueWorkStatus.NeedsInput)
        {
            return;
        }

        if (registry.Find(key) is { HasExited: false })
        {
            return;
        }

        tracker.Report(key, new IssueWorkState(IssueWorkStatus.Preparing));

        try
        {
            var worktree = await provisioner.ProvisionAsync(issue, cancellationToken);

            var (session, launch) = await StartClaudeAsync(key, worktree, cancellationToken);

            registry.Register(key, session);

            tracker.Report(key, new IssueWorkState(StatusOnceOpen(launch), worktree.BranchName, worktree.Path));

            _ = ReportWhenEndedAsync(key, session, worktree);
        }
        catch (KontextException ex)
        {
            tracker.Report(key, new IssueWorkState(IssueWorkStatus.Failed, before.BranchName, before.WorktreePath, ex.Message));
        }
        catch (OperationCanceledException)
        {
            tracker.Report(key, before);
            throw;
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            tracker.Report(key, new IssueWorkState(IssueWorkStatus.Failed, before.BranchName, before.WorktreePath, ex.Message));
        }
    }

    private async Task<(ITerminalSession Session, ClaudeLaunch Launch)> StartClaudeAsync(IssueKey key, WorktreeInfo worktree, CancellationToken cancellationToken)
    {
        var launch = claude.Prepare(sessions.Find(key), worktree);

        var session = await terminals.StartAsync(new TerminalSessionStart(launch.SessionId, launch.Options), cancellationToken);

        try
        {
            sessions.Save(new StoredSession(key, launch.SessionId, worktree.Path, worktree.BranchName));
        }
        catch
        {
            terminals.Close(launch.SessionId);
            throw;
        }

        return (session, launch);
    }

    private static IssueWorkStatus StatusOnceOpen(ClaudeLaunch launch)
        => !launch.ReportsItself ? IssueWorkStatus.Running
            : launch.Resumed ? IssueWorkStatus.NeedsInput
            : IssueWorkStatus.Ready;

    private async Task ReportWhenEndedAsync(IssueKey key, ITerminalSession session, WorktreeInfo worktree)
    {
        int? exitCode;
        try
        {
            exitCode = await session.Exited;
        }
#pragma warning disable CA1031
        catch (Exception)
#pragma warning restore CA1031
        {
            exitCode = -1;
        }

        await ui.RunAsync(() =>
        {
            if (ReferenceEquals(registry.Find(key), session))
            {
                tracker.Report(key, StateAfterExit(tracker.StateOf(key), exitCode, worktree));
            }
        });
    }

    private static IssueWorkState StateAfterExit(IssueWorkState current, int? exitCode, WorktreeInfo worktree)
        => exitCode is null or 0
            ? new IssueWorkState(
                current.Status == IssueWorkStatus.Done ? IssueWorkStatus.Done : IssueWorkStatus.Ready,
                worktree.BranchName,
                worktree.Path,
                SessionEnded: true)
            : new IssueWorkState(
                IssueWorkStatus.Failed,
                worktree.BranchName,
                worktree.Path,
                $"Claude exited with code {exitCode}.",
                SessionEnded: true);
}
