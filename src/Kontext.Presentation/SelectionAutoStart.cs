using CommunityToolkit.Mvvm.ComponentModel;
using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Presentation;

public sealed partial class SelectionAutoStart(
    IIssueSessionCoordinator coordinator,
    IIssueWorkTracker tracker,
    IIssueSessionRegistry registry,
    TimeProvider clock,
    AutoStartOptions options) : ObservableObject, IDisposable
{
    private CancellationTokenSource? _pending;

    [ObservableProperty]
    public partial IssueKey? PendingIssue { get; set; }

    public void Dispose()
    {
        _pending?.Cancel();
        _pending = null;
    }

    public void OnSelected(GitHubIssue? issue)
    {
        _pending?.Cancel();
        _pending = null;
        PendingIssue = null;

        if (issue is null || !ShouldStart(issue))
        {
            return;
        }

        var cancellation = _pending = new CancellationTokenSource();
        PendingIssue = issue.Key;
        _ = StartAfterPauseAsync(issue, cancellation);
    }

    private bool ShouldStart(GitHubIssue issue)
    {
        var key = issue.Key;
        var state = tracker.StateOf(key);

        return state.Status is IssueWorkStatus.NotStarted or IssueWorkStatus.Ready
            && !state.SessionEnded
            && registry.Find(key) is not { HasExited: false };
    }

    private async Task StartAfterPauseAsync(GitHubIssue issue, CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(options.Delay, clock, cancellation.Token);

            if (!ReferenceEquals(_pending, cancellation))
            {
                return;
            }

            _pending = null;
            PendingIssue = null;

            if (ShouldStart(issue))
            {
                await coordinator.StartOrResumeAsync(issue);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            cancellation.Dispose();
        }
    }
}
