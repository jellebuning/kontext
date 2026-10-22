using Kontext.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace Kontext.Presentation.Tests;

[TestFixture]
public class WorkspaceViewModelTests
{
    private static readonly TimeSpan Pause = TimeSpan.FromSeconds(1);

    private InMemoryIssueWorkTracker _tracker = null!;
    private FakeCoordinator _coordinator = null!;
    private FakeTimeProvider _clock = null!;
    private SelectionAutoStart _autoStart = null!;
    private IssueSidebarViewModel _sidebar = null!;
    private IssueDetailViewModel _detail = null!;
    private WorkspaceViewModel _workspace = null!;

    [SetUp]
    public async Task CreateWorkspace()
    {
        _tracker = new InMemoryIssueWorkTracker();
        _coordinator = new FakeCoordinator();
        _clock = new FakeTimeProvider();
        _autoStart = new SelectionAutoStart(_coordinator, _tracker, new InMemoryIssueSessionRegistry(), _clock, new AutoStartOptions(Pause));

        var gitHub = new FakeGitHubService
        {
            Issues = [Issues.Create(4, "Add notifications", "acme/memento"), Issues.Create(3, "Bring project under UI test", "acme/memento")],
        };

        _sidebar = new IssueSidebarViewModel(gitHub, _tracker, new FakeExistingWorkspaceFinder(), new WorkspaceIssues(gitHub), _clock);
        _detail = new IssueDetailViewModel(_coordinator, new FakeSessionClearer(), new FakeConfirmation(), new FakeWorkspaceOpener(), new InMemoryIssueSessionRegistry(), _autoStart);
        _workspace = new WorkspaceViewModel(_sidebar, _detail, _autoStart);

        await _sidebar.RefreshCommand.ExecuteAsync(null);
    }

    [TearDown]
    public void Dispose()
    {
        _detail.Dispose();
        _sidebar.Dispose();
        _autoStart.Dispose();
    }

    private IssueItemViewModel ItemNumbered(int number) => _sidebar.Groups.SelectMany(group => group.Items).Single(item => item.Issue.Number == number);

    [Test]
    public void ShowsTheSidebarSelectionInTheDetailPane()
    {
        Assert.That(_workspace.Detail.HasSelection, Is.False);

        _sidebar.SelectedItem = ItemNumbered(4);

        Assert.That(_workspace.Detail.Title, Is.EqualTo("Add notifications"));

        _sidebar.SelectedItem = null;

        Assert.That(_workspace.Detail.HasSelection, Is.False);
    }

    [Test]
    public void SelectingAnIssueSchedulesClaudeToStartOnItAfterThePause()
    {
        _sidebar.SelectedItem = ItemNumbered(4);

        Assert.That(_autoStart.PendingIssue, Is.EqualTo(ItemNumbered(4).Key));
        Assert.That(_workspace.Detail.IsPending, Is.True);
        Assert.That(_coordinator.Started, Is.Empty);
    }

    [Test]
    public async Task StartsOnTheIssueTheUserSettlesOn()
    {
        _sidebar.SelectedItem = ItemNumbered(4);
        _clock.Advance(Pause);

        await AsyncAssert.EventuallyAsync(() => _coordinator.Started.Count == 1, "the start");
        Assert.That(_coordinator.Started.Single().Number, Is.EqualTo(4));
    }

    [Test]
    public async Task MovingToAnotherIssueBeforeThePauseEndsStartsOnlyTheSecond()
    {
        _sidebar.SelectedItem = ItemNumbered(4);
        _clock.Advance(TimeSpan.FromMilliseconds(500));
        _sidebar.SelectedItem = ItemNumbered(3);
        _clock.Advance(Pause);

        await AsyncAssert.EventuallyAsync(() => _coordinator.Started.Count == 1, "the start");
        await Task.Delay(50);
        Assert.That(_coordinator.Started.Single().Number, Is.EqualTo(3));
    }

    [Test]
    public async Task ClearingTheSelectionCancelsThePendingStart()
    {
        _sidebar.SelectedItem = ItemNumbered(4);

        _sidebar.SelectedItem = null;
        _clock.Advance(Pause * 2);
        await Task.Delay(50);

        Assert.That(_coordinator.Started, Is.Empty);
    }
}
