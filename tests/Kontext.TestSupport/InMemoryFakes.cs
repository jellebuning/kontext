using System.Collections.Concurrent;
using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.TestSupport;

public sealed class InMemoryIssueWorkTracker : IIssueWorkTracker
{
    private readonly ConcurrentDictionary<IssueKey, IssueWorkState> _states = new();

    public event EventHandler<IssueWorkChangedEventArgs>? Changed;

    public IssueWorkState StateOf(IssueKey issue) => _states.GetValueOrDefault(issue, IssueWorkState.NotStarted);

    public void Report(IssueKey issue, IssueWorkState state)
    {
        _states[issue] = state;
        Changed?.Invoke(this, new IssueWorkChangedEventArgs(issue, state));
    }
}

public sealed class InMemoryIssueSessionRegistry : IIssueSessionRegistry
{
    private readonly ConcurrentDictionary<IssueKey, ITerminalSession> _sessions = new();

    public event EventHandler<IssueSessionChangedEventArgs>? Changed;

    public ITerminalSession? Find(IssueKey issue) => _sessions.GetValueOrDefault(issue);

    public IssueKey? IssueOf(TerminalSessionId session)
    {
        foreach (var (issue, registered) in _sessions)
        {
            if (registered.Id == session)
            {
                return issue;
            }
        }

        return null;
    }

    public void Register(IssueKey issue, ITerminalSession session)
    {
        _sessions[issue] = session;
        Changed?.Invoke(this, new IssueSessionChangedEventArgs(issue, session));
    }

    public void Remove(IssueKey issue) => _sessions.TryRemove(issue, out _);
}

public sealed class FakeClaudeLauncher : IClaudeLauncher
{
    public bool ReportsItself { get; set; } = true;

    public bool Resumed { get; set; }

    public ClaudeLaunch? Next { get; set; }

    public List<(StoredSession? Earlier, WorktreeInfo Worktree)> Calls { get; } = [];

    public ClaudeLaunch Prepare(StoredSession? earlier, WorktreeInfo worktree)
    {
        Calls.Add((earlier, worktree));

        return Next ?? new ClaudeLaunch(
            earlier?.SessionId ?? TerminalSessionId.New(),
            new TerminalSessionOptions(worktree.Path, "claude", ["--fake"]),
            ReportsItself,
            Resumed);
    }
}
