using Kontext.Domain;
using Kontext.Kernel;
using Kontext.TestSupport;

namespace Kontext.Sessions.UnitTests;

[TestFixture]
public class IssueSessionClearerTests
{
    private static readonly WorktreeInfo Worktree = new(@"C:\wt\4-add", "features/4-add", "abc", IsMain: false);

    private readonly GitHubIssue _issue = Issues.Create(4, "Add notifications", "acme/memento");
    private InMemoryIssueWorkTracker _tracker = null!;
    private FakeSessionStore _store = null!;
    private FakeTerminalService _terminals = null!;
    private InMemoryIssueSessionRegistry _registry = null!;
    private FakeWorkspaceRemover _workspaces = null!;
    private IssueSessionClearer _clearer = null!;

    private IssueKey Key => _issue.Key;

    [SetUp]
    public void CreateFakes()
    {
        _tracker = new InMemoryIssueWorkTracker();
        _store = new FakeSessionStore();
        _terminals = new FakeTerminalService();
        _registry = new InMemoryIssueSessionRegistry();
        _workspaces = new FakeWorkspaceRemover();

        _clearer = new IssueSessionClearer(_tracker, _store, _terminals, _registry, _workspaces, TimeProvider.System);
    }

    [TearDown]
    public void DisposeFakes() => _terminals.Dispose();

    private async Task<FakeTerminalSession> ASessionIsOpenAsync()
    {
        var id = TerminalSessionId.New();
        var session = (FakeTerminalSession)await _terminals.StartAsync(new TerminalSessionStart(id, new TerminalSessionOptions(Worktree.Path, "claude", [])));
        _registry.Register(Key, session);
        _store.Save(new StoredSession(Key, id, Worktree.Path, Worktree.BranchName));
        _tracker.Report(Key, new IssueWorkState(IssueWorkStatus.NeedsInput, Worktree.BranchName, Worktree.Path));
        return session;
    }

    [Test]
    public async Task EndsTheSessionRemovesTheWorktreeAndBranchAndForgetsTheIssue()
    {
        var session = await ASessionIsOpenAsync();

        await _clearer.ClearAsync(_issue);

        Assert.That(_terminals.Closed, Is.EqualTo(Expect.Seq(session.Id)));
        Assert.That(_registry.Find(Key), Is.Null);
        Assert.That(_workspaces.Removed, Is.EqualTo(Expect.Seq((_issue.Repository, Worktree.Path, Worktree.BranchName))));
        Assert.That(_store.Find(Key), Is.Null);
        Assert.That(_tracker.StateOf(Key), Is.EqualTo(IssueWorkState.NotStarted));
    }

    [Test]
    public async Task ClearsAnIssueWhoseSessionIsNotOpenFromWhatWasStored()
    {
        _store.Save(new StoredSession(Key, TerminalSessionId.New(), Worktree.Path, Worktree.BranchName));
        _tracker.Report(Key, new IssueWorkState(IssueWorkStatus.Done));

        await _clearer.ClearAsync(_issue);

        Assert.That(_terminals.Closed, Is.Empty);
        Assert.That(_workspaces.Removed.Single().BranchName, Is.EqualTo("features/4-add"));
        Assert.That(_tracker.StateOf(Key), Is.EqualTo(IssueWorkState.NotStarted));
    }

    [Test]
    public async Task RemovesNothingFromGitWhenThereWasNoWorkspace()
    {
        await _clearer.ClearAsync(_issue);

        Assert.That(_workspaces.Removed, Is.Empty);
        Assert.That(_tracker.StateOf(Key), Is.EqualTo(IssueWorkState.NotStarted));
    }

    [Test]
    public async Task ReportsFailedAndKeepsTheStoredSessionWhenGitCannotRemoveTheWorkspace()
    {
        await ASessionIsOpenAsync();
        _workspaces.FailWith = new TestKontextException("the folder is in use");

        await _clearer.ClearAsync(_issue);

        var state = _tracker.StateOf(Key);
        Assert.That(state.Status, Is.EqualTo(IssueWorkStatus.Failed));
        Assert.That(state.Message, Is.EqualTo("the folder is in use"));
        Assert.That(state.WorktreePath, Is.EqualTo(Worktree.Path));
        Assert.That(_store.Find(Key), Is.Not.Null);
    }
}
