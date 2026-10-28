using Kontext.Domain;
using Kontext.Kernel;
using Kontext.TestSupport;

namespace Kontext.Sessions.UnitTests;

[TestFixture]
public class IssueSessionCoordinatorTests
{
    private static readonly WorktreeInfo Worktree = new(@"C:\wt\4-add", "features/4-add", "abc", IsMain: false);

    private readonly GitHubIssue _issue = Issues.Create(4, "Add notifications", "acme/memento");
    private IssueWorkTracker _tracker = null!;
    private FakeProvisioner _provisioner = null!;
    private FakeSessionStore _store = null!;
    private FakeTerminalService _terminals = null!;
    private FakeClaudeLauncher _claude = null!;
    private IssueSessionRegistry _registry = null!;
    private IssueSessionCoordinator _coordinator = null!;

    private IssueKey Key => _issue.Key;

    [SetUp]
    public void CreateFakes()
    {
        _tracker = new IssueWorkTracker();
        _provisioner = new FakeProvisioner { Result = Worktree };
        _store = new FakeSessionStore();
        _terminals = new FakeTerminalService();
        _claude = new FakeClaudeLauncher();
        _registry = new IssueSessionRegistry();

        _coordinator = new IssueSessionCoordinator(
            _provisioner,
            _tracker,
            _store,
            _terminals,
            _registry,
            _claude,
            new ImmediateDispatcher());
    }

    [TearDown]
    public void DisposeFakes() => _terminals.Dispose();

    private Task WaitForStatus(IssueWorkStatus status)
        => AsyncAssert.EventuallyAsync(() => _tracker.StateOf(Key).Status == status, $"status {status}");

    private Task WaitForEnded()
        => AsyncAssert.EventuallyAsync(() => _tracker.StateOf(Key).SessionEnded, "the session to be reported as ended");

    [Test]
    public async Task ReportsPreparingThenReadyBecauseNothingIsRunningUntilTheUserWrites()
    {
        var seen = _tracker.Record(Key);

        await _coordinator.StartOrResumeAsync(_issue);

        Assert.That(seen, Is.EqualTo(Expect.Seq(IssueWorkStatus.Preparing, IssueWorkStatus.Ready)));
        var state = _tracker.StateOf(Key);
        Assert.That(state.BranchName, Is.EqualTo("features/4-add"));
        Assert.That(state.WorktreePath, Is.EqualTo(@"C:\wt\4-add"));
        Assert.That(_registry.Find(Key), Is.SameAs(_terminals.LastSession));
    }

    [Test]
    public async Task ReportsThatAContinuedConversationNeedsInputBecauseItIsWaitingForTheNextMessage()
    {
        _claude.Resumed = true;
        var seen = _tracker.Record(Key);

        await _coordinator.StartOrResumeAsync(_issue);

        Assert.That(seen, Is.EqualTo(Expect.Seq(IssueWorkStatus.Preparing, IssueWorkStatus.NeedsInput)));
        var state = _tracker.StateOf(Key);
        Assert.That(state.BranchName, Is.EqualTo("features/4-add"));
        Assert.That(state.SessionEnded, Is.False);
    }

    [Test]
    public async Task ReportsRunningForAContinuedConversationThatCannotReportWhatItDoes()
    {
        _claude.Resumed = true;
        _claude.ReportsItself = false;

        await _coordinator.StartOrResumeAsync(_issue);

        Assert.That(_tracker.StateOf(Key).Status, Is.EqualTo(IssueWorkStatus.Running));
    }

    [Test]
    public async Task ReportsRunningWhenTheSessionCannotReportWhatItDoes()
    {
        _claude.ReportsItself = false;

        await _coordinator.StartOrResumeAsync(_issue);

        Assert.That(_tracker.StateOf(Key).Status, Is.EqualTo(IssueWorkStatus.Running), "a live process is all that is known");
    }

    [Test]
    public async Task StartsTheProcessTheLauncherPreparedUnderItsSessionId()
    {
        await _coordinator.StartOrResumeAsync(_issue);

        var start = _terminals.Starts.Single();
        Assert.That(start.Options.WorkingDirectory, Is.EqualTo(@"C:\wt\4-add"));
        Assert.That(start.Options.Arguments, Is.EqualTo(Expect.Seq("--fake")));
    }

    [Test]
    public async Task UsesTheSessionIdTheLauncherChose()
    {
        var chosen = TerminalSessionId.New();
        _claude.Next = new ClaudeLaunch(chosen, new TerminalSessionOptions(@"C:\wt\4-add", "claude", []), ReportsItself: true, Resumed: false);

        await _coordinator.StartOrResumeAsync(_issue);

        Assert.That(_terminals.Starts.Single().Id, Is.EqualTo(chosen));
        Assert.That(_store.Saved[Key].SessionId, Is.EqualTo(chosen));
    }

    [Test]
    public async Task AsksTheLauncherWithTheStoredSessionAndTheWorkspace()
    {
        var stored = new StoredSession(Key, TerminalSessionId.New(), @"C:\wt\4-add", "features/4-add");
        _store.Saved[Key] = stored;

        await _coordinator.StartOrResumeAsync(_issue);

        var (earlier, worktree) = _claude.Calls.Single();
        Assert.That(earlier, Is.EqualTo(stored));
        Assert.That(worktree, Is.EqualTo(Worktree));
    }

    [Test]
    public async Task AsksTheLauncherWithNoEarlierSessionWhenNothingWasStored()
    {
        await _coordinator.StartOrResumeAsync(_issue);

        Assert.That(_claude.Calls.Single().Earlier, Is.Null);
    }

    [Test]
    public async Task RemembersTheSessionSoItCanBeResumedLater()
    {
        await _coordinator.StartOrResumeAsync(_issue);

        var stored = _store.Saved[Key];
        Assert.That(stored.SessionId, Is.EqualTo(_terminals.Starts.Single().Id));
        Assert.That(stored.WorktreePath, Is.EqualTo(@"C:\wt\4-add"));
        Assert.That(stored.BranchName, Is.EqualTo("features/4-add"));
    }

    [Test]
    public async Task ReportsTheEnvironmentReadyAndTheSessionEndedWhenClaudeExitsWithoutFinishingAnything()
    {
        await _coordinator.StartOrResumeAsync(_issue);

        _terminals.LastSession!.End(0);

        await WaitForEnded();
        var state = _tracker.StateOf(Key);
        Assert.That(state.Status, Is.EqualTo(IssueWorkStatus.Ready));
        Assert.That(state.BranchName, Is.EqualTo("features/4-add"), "the workspace stays visible after the session ends");
        Assert.That(state.WorktreePath, Is.EqualTo(@"C:\wt\4-add"));
    }

    [Test]
    public async Task FallsBackToTheEnvironmentReadyWhenClaudeExitsWhileWaitingForTheUser()
    {
        await _coordinator.StartOrResumeAsync(_issue);
        _tracker.Report(Key, _tracker.StateOf(Key) with { Status = IssueWorkStatus.NeedsInput });

        _terminals.LastSession!.End(0);

        await WaitForEnded();
        Assert.That(_tracker.StateOf(Key).Status, Is.EqualTo(IssueWorkStatus.Ready));
    }

    [Test]
    public async Task KeepsDoneBecauseTheIssueIsClosedWhenClaudeExits()
    {
        await _coordinator.StartOrResumeAsync(_issue);
        _tracker.Report(Key, _tracker.StateOf(Key) with { Status = IssueWorkStatus.Done });

        _terminals.LastSession!.End(0);

        await WaitForEnded();
        Assert.That(_tracker.StateOf(Key).Status, Is.EqualTo(IssueWorkStatus.Done));
    }

    [Test]
    public async Task DoesNotCallItDoneWhenClaudeExitedInTheMiddleOfWorking()
    {
        await _coordinator.StartOrResumeAsync(_issue);
        _tracker.Report(Key, _tracker.StateOf(Key) with { Status = IssueWorkStatus.Running });

        _terminals.LastSession!.End(0);

        await WaitForEnded();
        Assert.That(_tracker.StateOf(Key).Status, Is.EqualTo(IssueWorkStatus.Ready));
    }

    [Test]
    public async Task TreatsAnExitCodeThatCannotBeReadAsACleanExit()
    {
        await _coordinator.StartOrResumeAsync(_issue);

        _terminals.LastSession!.End(null);

        await WaitForEnded();
        Assert.That(_tracker.StateOf(Key).Status, Is.EqualTo(IssueWorkStatus.Ready));
    }

    [Test]
    public async Task ReportsFailedWithTheExitCodeWhenClaudeExitsWithAnError()
    {
        await _coordinator.StartOrResumeAsync(_issue);

        _terminals.LastSession!.End(3);

        await WaitForStatus(IssueWorkStatus.Failed);
        Assert.That(_tracker.StateOf(Key).Message, Is.EqualTo("Claude exited with code 3."));
        Assert.That(_tracker.StateOf(Key).SessionEnded, Is.True);
    }

    [Test]
    public async Task ReportsFailedWhenTheEndOfTheSessionCannotBeObserved()
    {
        await _coordinator.StartOrResumeAsync(_issue);

        _terminals.LastSession!.Crash(new InvalidOperationException("pty vanished"));

        await WaitForStatus(IssueWorkStatus.Failed);
    }

    [Test]
    public async Task DoesNothingWhileTheIssueIsAlreadyBeingPreparedOrWorkedOn()
    {
        foreach (var status in new[] { IssueWorkStatus.Preparing, IssueWorkStatus.Running, IssueWorkStatus.NeedsInput })
        {
            _tracker.Report(Key, new IssueWorkState(status));

            await _coordinator.StartOrResumeAsync(_issue);

            Assert.That(_terminals.Starts, Is.Empty, $"{status} must not start another session");
            Assert.That(_tracker.StateOf(Key).Status, Is.EqualTo(status));
        }
    }

    [TestCase(IssueWorkStatus.NotStarted)]
    [TestCase(IssueWorkStatus.Ready)]
    [TestCase(IssueWorkStatus.Done)]
    [TestCase(IssueWorkStatus.Failed)]
    public async Task StartsFromEveryRestingState(IssueWorkStatus before)
    {
        _tracker.Report(Key, new IssueWorkState(before));

        await _coordinator.StartOrResumeAsync(_issue);

        Assert.That(_tracker.StateOf(Key).Status, Is.EqualTo(IssueWorkStatus.Ready));
    }

    [TestCase(IssueWorkStatus.Ready)]
    [TestCase(IssueWorkStatus.Done)]
    public async Task NeverStartsASecondSessionWhileTheFirstIsStillOpen(IssueWorkStatus whileOpen)
    {
        await _coordinator.StartOrResumeAsync(_issue);
        _tracker.Report(Key, _tracker.StateOf(Key) with { Status = whileOpen });

        await _coordinator.StartOrResumeAsync(_issue);

        Assert.That(_terminals.Starts, Has.Count.EqualTo(1));
        Assert.That(_tracker.StateOf(Key).Status, Is.EqualTo(whileOpen));
    }

    [Test]
    public async Task StartsAgainAfterTheSessionEndedHandingTheLauncherTheRememberedSession()
    {
        await _coordinator.StartOrResumeAsync(_issue);
        var firstId = _terminals.Starts.Single().Id;
        _terminals.LastSession!.End(1);
        await WaitForStatus(IssueWorkStatus.Failed);

        await _coordinator.StartOrResumeAsync(_issue);

        Assert.That(_terminals.Starts, Has.Count.EqualTo(2));
        Assert.That(_claude.Calls[1].Earlier?.SessionId, Is.EqualTo(firstId), "the launcher decides whether that session can be resumed");
        Assert.That(_tracker.StateOf(Key).Status, Is.EqualTo(IssueWorkStatus.Ready));
        Assert.That(_tracker.StateOf(Key).SessionEnded, Is.False, "a restarted session is open again");
        Assert.That(_registry.Find(Key), Is.SameAs(_terminals.LastSession), "the registry now points at the new session");
    }

    [Test]
    public async Task ReportsFailedWithTheMessageWhenThereIsNoWorkspace()
    {
        _provisioner.FailWith = new TestKontextException("git exploded");

        await _coordinator.StartOrResumeAsync(_issue);

        var state = _tracker.StateOf(Key);
        Assert.That(state.Status, Is.EqualTo(IssueWorkStatus.Failed));
        Assert.That(state.Message, Is.EqualTo("git exploded"));
        Assert.That(_terminals.Starts, Is.Empty);
    }

    [Test]
    public async Task KeepsTheWorkspaceItAlreadyKnewWhenProvisioningFailsAgain()
    {
        _tracker.Report(Key, new IssueWorkState(IssueWorkStatus.Failed, "features/4-add", @"C:\wt\4-add", "earlier", SessionEnded: true));
        _provisioner.FailWith = new TestKontextException("worktree add failed");

        await _coordinator.StartOrResumeAsync(_issue);

        var state = _tracker.StateOf(Key);
        Assert.That(state.Message, Is.EqualTo("worktree add failed"));
        Assert.That(state.BranchName, Is.EqualTo("features/4-add"));
        Assert.That(state.WorktreePath, Is.EqualTo(@"C:\wt\4-add"));
    }

    [Test]
    public async Task ReportsFailedWhenTheTerminalCannotBeStartedAndRemembersNothing()
    {
        _terminals.FailWith = new TerminalStartException("The terminal did not start in time.");

        await _coordinator.StartOrResumeAsync(_issue);

        var state = _tracker.StateOf(Key);
        Assert.That(state.Status, Is.EqualTo(IssueWorkStatus.Failed));
        Assert.That(state.Message, Does.Contain("did not start"));
        Assert.That(_store.Saved, Is.Empty);
        Assert.That(_registry.Find(Key), Is.Null);
    }

    [Test]
    public async Task ReportsFailedAndClosesTheTerminalWhenTheSessionCannotBeSaved()
    {
        _store.SaveFailsWith = new IOException("sessions.json is locked");

        await _coordinator.StartOrResumeAsync(_issue);

        var state = _tracker.StateOf(Key);
        Assert.That(state.Status, Is.EqualTo(IssueWorkStatus.Failed));
        Assert.That(state.Message, Is.EqualTo("sessions.json is locked"));
        Assert.That(_terminals.Closed, Has.Count.EqualTo(1), "the terminal that was started must not be left running");
        Assert.That(_registry.Find(Key), Is.Null);
    }

    [Test]
    public async Task CanBeStartedAgainAfterAnUnexpectedFailure()
    {
        _store.SaveFailsWith = new IOException("locked");
        await _coordinator.StartOrResumeAsync(_issue);
        _store.SaveFailsWith = null;

        await _coordinator.StartOrResumeAsync(_issue);

        Assert.That(_tracker.StateOf(Key).Status, Is.EqualTo(IssueWorkStatus.Ready));
    }

    [Test]
    public void RestoresTheStateAndRethrowsWhenCancelled()
    {
        _provisioner.FailWith = new OperationCanceledException();

        Assert.That(async () => await _coordinator.StartOrResumeAsync(_issue), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(_tracker.StateOf(Key).Status, Is.EqualTo(IssueWorkStatus.NotStarted));
    }

    [Test]
    public async Task ReportsAnUnexpectedExceptionAsFailedInsteadOfLeavingTheIssuePreparingForever()
    {
        _provisioner.FailWith = new InvalidOperationException("bug");

        await _coordinator.StartOrResumeAsync(_issue);

        Assert.That(_tracker.StateOf(Key).Status, Is.EqualTo(IssueWorkStatus.Failed));
        Assert.That(_tracker.StateOf(Key).Message, Is.EqualTo("bug"));
    }

    [Test]
    public void RefusesANullIssue()
    {
        Assert.That(async () => await _coordinator.StartOrResumeAsync(null!), Throws.ArgumentNullException);
    }

    [Test]
    public async Task SaysNothingWhenASessionEndsAfterItWasTakenOutOfTheRegistry()
    {
        await _coordinator.StartOrResumeAsync(_issue);
        var session = _terminals.LastSession!;
        _registry.Remove(Key);
        _tracker.Report(Key, IssueWorkState.NotStarted);

        session.End(1);
        await Task.Delay(100);

        Assert.That(_tracker.StateOf(Key), Is.EqualTo(IssueWorkState.NotStarted));
    }
}
