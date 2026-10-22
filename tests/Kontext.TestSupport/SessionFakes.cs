using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.TestSupport;

public sealed class FakeExistingWorkspaceFinder : IExistingWorkspaceFinder
{
    public List<ExistingWorkspace> Found { get; } = [];

    public Task<IReadOnlyList<ExistingWorkspace>> FindAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ExistingWorkspace>>(Found.ToList());
}

public sealed class FakeProvisioner : IWorkspaceProvisioner
{
    public WorktreeInfo Result { get; set; } = new(@"C:\wt\default", "kontext/0-default", "abc", IsMain: false);

    public Exception? FailWith { get; set; }

    public Task<WorktreeInfo> ProvisionAsync(GitHubIssue issue, CancellationToken cancellationToken = default)
        => FailWith is null ? Task.FromResult(Result) : Task.FromException<WorktreeInfo>(FailWith);
}

public sealed class FakeCoordinator : IIssueSessionCoordinator
{
    public List<GitHubIssue> Started { get; } = [];

    public Task StartOrResumeAsync(GitHubIssue issue, CancellationToken cancellationToken = default)
    {
        Started.Add(issue);
        return Task.CompletedTask;
    }
}

public sealed class FakeTerminalSession(TerminalSessionId id) : ITerminalSession
{
    private readonly TaskCompletionSource<int?> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TerminalSessionId Id { get; } = id;

    public bool HasExited => _exited.Task.IsCompleted;

    public Task<int?> Exited => _exited.Task;

    public List<string> Written { get; } = [];

    public void Write(string text) => Written.Add(text);

    public void End(int? exitCode) => _exited.SetResult(exitCode);

    public void Crash(Exception error) => _exited.SetException(error);
}

public sealed class FakeTerminalService : ITerminalSessionService
{
    public List<TerminalSessionStart> Starts { get; } = [];

    public Exception? FailWith { get; set; }

    public FakeTerminalSession? LastSession { get; private set; }

    public List<TerminalSessionId> Closed { get; } = [];

    public Task<ITerminalSession> StartAsync(TerminalSessionStart start, CancellationToken cancellationToken = default)
    {
        Starts.Add(start);

        if (FailWith is not null)
        {
            return Task.FromException<ITerminalSession>(FailWith);
        }

        LastSession = new FakeTerminalSession(start.Id);
        return Task.FromResult<ITerminalSession>(LastSession);
    }

    public ITerminalSession? Find(TerminalSessionId id) => LastSession is { } session && session.Id == id ? session : null;

    public void Close(TerminalSessionId id)
    {
        Closed.Add(id);

        if (LastSession is { HasExited: false } session && session.Id == id)
        {
            session.End(null);
        }
    }

    public void Dispose()
    {
    }
}

public sealed class FakeSessionStore : ISessionStore
{
    public Dictionary<IssueKey, StoredSession> Saved { get; } = [];

    public StoredSession? Find(IssueKey issue) => Saved.GetValueOrDefault(issue);

    public Exception? SaveFailsWith { get; set; }

    public void Save(StoredSession session)
    {
        if (SaveFailsWith is not null)
        {
            throw SaveFailsWith;
        }

        Saved[session.Issue] = session;
    }

    public void Remove(IssueKey issue) => Saved.Remove(issue);
}

public sealed class ImmediateDispatcher : IUiDispatcher
{
    public bool HasThreadAccess => true;

    public Task<T> RunAsync<T>(Func<T> work) => Task.FromResult(work());

    public Task RunAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}

public sealed class FakeSessionClearer : IIssueSessionClearer
{
    public List<GitHubIssue> Cleared { get; } = [];

    public Task ClearAsync(GitHubIssue issue, CancellationToken cancellationToken = default)
    {
        Cleared.Add(issue);
        return Task.CompletedTask;
    }
}

public sealed class FakeWorkspaceRemover : IWorkspaceRemover
{
    public List<(RepositoryRef Repository, string WorktreePath, string BranchName)> Removed { get; } = [];

    public Exception? FailWith { get; set; }

    public Task RemoveAsync(RepositoryRef repository, string worktreePath, string branchName, CancellationToken cancellationToken = default)
    {
        Removed.Add((repository, worktreePath, branchName));
        return FailWith is null ? Task.CompletedTask : Task.FromException(FailWith);
    }
}

public sealed class FakeWorkspaceOpener : IWorkspaceOpener
{
    public List<(WorkspaceTool Tool, string Folder)> Opened { get; } = [];

    public Exception? FailWith { get; set; }

    public void Open(WorkspaceTool tool, string folder)
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }

        Opened.Add((tool, folder));
    }
}

public sealed class FakeConfirmation : IConfirmation
{
    private TaskCompletionSource<bool>? _pending;

    public bool Answer { get; set; } = true;

    public bool Deferred { get; set; }

    public List<(string Title, string Message, string ConfirmText)> Asked { get; } = [];

    public Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        Asked.Add((title, message, confirmText));

        if (!Deferred)
        {
            return Task.FromResult(Answer);
        }

        _pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _pending.Task;
    }

    public void Respond(bool answer) => _pending!.SetResult(answer);
}