using Kontext.Domain;
using Kontext.Kernel;
using Kontext.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace Kontext.Presentation.UnitTests;

[TestFixture]
public class SelectionAutoStartTests
{
    private static readonly TimeSpan Pause = TimeSpan.FromSeconds(1);

    private readonly GitHubIssue _first = Issues.Create(1, "First", "acme/app");
    private readonly GitHubIssue _second = Issues.Create(2, "Second", "acme/app");
    private FakeTimeProvider _clock = null!;
    private InMemoryIssueWorkTracker _tracker = null!;
    private InMemoryIssueSessionRegistry _registry = null!;
    private FakeCoordinator _coordinator = null!;
    private SelectionAutoStart _autoStart = null!;

    [SetUp]
    public void CreateFakes()
    {
        _clock = new FakeTimeProvider();
        _tracker = new InMemoryIssueWorkTracker();
        _registry = new InMemoryIssueSessionRegistry();
        _coordinator = new FakeCoordinator();
        _autoStart = new SelectionAutoStart(_coordinator, _tracker, _registry, _clock, new AutoStartOptions(Pause));
    }

    [TearDown]
    public void DisposeAutoStart() => _autoStart.Dispose();

    private Task WaitForStarts(int count)
        => AsyncAssert.EventuallyAsync(() => _coordinator.Started.Count >= count, $"{count} start(s)");

    [Test]
    public void WaitsOutThePauseBeforeStartingAndSaysSo()
    {
        _autoStart.OnSelected(_first);

        Assert.That(_autoStart.PendingIssue, Is.EqualTo(_first.Key));
        Assert.That(_coordinator.Started, Is.Empty);

        _clock.Advance(Pause - TimeSpan.FromMilliseconds(1));

        Assert.That(_coordinator.Started, Is.Empty, "one millisecond short of the pause");
    }

    [Test]
    public async Task StartsOnceTheSelectionHasRestedForThePause()
    {
        _autoStart.OnSelected(_first);

        _clock.Advance(Pause);

        await WaitForStarts(1);
        Assert.That(_coordinator.Started.Single(), Is.SameAs(_first));
        await AsyncAssert.EventuallyAsync(() => _autoStart.PendingIssue is null, "the pending note to clear");
    }

    [Test]
    public async Task ASelectionThatMovesOnWithinThePauseNeverStarts()
    {
        _autoStart.OnSelected(_first);
        _clock.Advance(TimeSpan.FromMilliseconds(600));

        _autoStart.OnSelected(_second);
        _clock.Advance(TimeSpan.FromMilliseconds(600));
        Assert.That(_coordinator.Started, Is.Empty, "neither has rested for a full pause yet");
        Assert.That(_autoStart.PendingIssue, Is.EqualTo(_second.Key));

        _clock.Advance(TimeSpan.FromMilliseconds(400));

        await WaitForStarts(1);
        Assert.That(_coordinator.Started.Single(), Is.SameAs(_second), "only the issue the user stopped on");
    }

    [Test]
    public async Task ScrollingPastManyIssuesStartsOnlyTheLastOne()
    {
        var issues = Enumerable.Range(1, 6).Select(number => Issues.Create(number, $"Issue {number}")).ToList();

        foreach (var issue in issues)
        {
            _autoStart.OnSelected(issue);
            _clock.Advance(TimeSpan.FromMilliseconds(200));
        }

        _clock.Advance(Pause);

        await WaitForStarts(1);
        await Task.Delay(50);
        Assert.That(_coordinator.Started.Select(started => started.Number), Is.EqualTo(Expect.Seq(6)));
    }

    [Test]
    public async Task ClearingTheSelectionCancelsThePendingStart()
    {
        _autoStart.OnSelected(_first);

        _autoStart.OnSelected(null);
        _clock.Advance(Pause * 2);
        await Task.Delay(50);

        Assert.That(_coordinator.Started, Is.Empty);
        Assert.That(_autoStart.PendingIssue, Is.Null);
    }

    [TestCase(IssueWorkStatus.Preparing)]
    [TestCase(IssueWorkStatus.Running)]
    [TestCase(IssueWorkStatus.NeedsInput)]
    [TestCase(IssueWorkStatus.Done)]
    [TestCase(IssueWorkStatus.Failed)]
    public async Task NeverAutoStartsAnIssueThatIsInFlightOrFinished(IssueWorkStatus status)
    {
        _tracker.Report(_first.Key, new IssueWorkState(status));

        _autoStart.OnSelected(_first);
        _clock.Advance(Pause * 2);
        await Task.Delay(50);

        Assert.That(_autoStart.PendingIssue, Is.Null);
        Assert.That(_coordinator.Started, Is.Empty);
    }

    [Test]
    public async Task NeverAutoStartsAnIssueWhoseSessionHasEnded()
    {
        _tracker.Report(_first.Key, new IssueWorkState(IssueWorkStatus.Ready, "kontext/1-first", @"C:\wt\1", SessionEnded: true));

        _autoStart.OnSelected(_first);
        _clock.Advance(Pause * 2);
        await Task.Delay(50);

        Assert.That(_autoStart.PendingIssue, Is.Null);
        Assert.That(_coordinator.Started, Is.Empty, "an ended session is resumed only when the user asks");
    }

    [Test]
    public async Task NeverAutoStartsAnIssueThatAlreadyHasAnOpenSession()
    {
        _tracker.Report(_first.Key, new IssueWorkState(IssueWorkStatus.Ready, "kontext/1-first", @"C:\wt\1"));
        _registry.Register(_first.Key, new FakeTerminalSession(TerminalSessionId.New()));

        _autoStart.OnSelected(_first);
        _clock.Advance(Pause * 2);
        await Task.Delay(50);

        Assert.That(_autoStart.PendingIssue, Is.Null);
        Assert.That(_coordinator.Started, Is.Empty);
    }

    [Test]
    public async Task StartsAnIssueWhoseWorktreeSurvivedARestart()
    {
        _tracker.Report(_first.Key, new IssueWorkState(IssueWorkStatus.Ready, "kontext/1-first", @"C:\wt\1"));

        _autoStart.OnSelected(_first);
        _clock.Advance(Pause);

        await WaitForStarts(1);
    }

    [Test]
    public async Task SkipsTheStartIfSomethingElseStartedTheIssueDuringThePause()
    {
        _autoStart.OnSelected(_first);

        _tracker.Report(_first.Key, new IssueWorkState(IssueWorkStatus.Running));
        _clock.Advance(Pause);
        await Task.Delay(50);

        Assert.That(_coordinator.Started, Is.Empty);
        Assert.That(_autoStart.PendingIssue, Is.Null);
    }

    [Test]
    public void RaisesPropertyChangedWhenThePendingIssueChanges()
    {
        var changes = new List<string?>();
        _autoStart.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        _autoStart.OnSelected(_first);

        Assert.That(changes, Does.Contain(nameof(SelectionAutoStart.PendingIssue)));
    }

    [Test]
    public async Task DisposalCancelsAPendingStart()
    {
        _autoStart.OnSelected(_first);

        _autoStart.Dispose();
        _clock.Advance(Pause * 2);
        await Task.Delay(50);

        Assert.That(_coordinator.Started, Is.Empty);
    }

    [Test]
    public async Task UsesTheConfiguredPause()
    {
        using var slow = new SelectionAutoStart(_coordinator, _tracker, new InMemoryIssueSessionRegistry(), _clock, new AutoStartOptions(TimeSpan.FromSeconds(5)));

        slow.OnSelected(_first);
        _clock.Advance(TimeSpan.FromSeconds(4));
        await Task.Delay(50);
        Assert.That(_coordinator.Started, Is.Empty);

        _clock.Advance(TimeSpan.FromSeconds(1));
        await WaitForStarts(1);
    }
}
