using Kontext.Domain;
using Kontext.Kernel;
using Kontext.TestSupport;

namespace Kontext.Presentation.UnitTests;

[TestFixture]
public class IssueSidebarViewModelTests
{
    private InMemoryIssueWorkTracker _tracker = null!;
    private FakeGitHubService _gitHub = null!;
    private FakeExistingWorkspaceFinder _finder = null!;
    private IssueSidebarViewModel _sidebar = null!;

    [SetUp]
    public void CreateSidebar()
    {
        _tracker = new InMemoryIssueWorkTracker();
        _gitHub = new FakeGitHubService();
        _finder = new FakeExistingWorkspaceFinder();

        _gitHub.Issues =
        [
            Issues.Create(4, "Add notifications", "acme/memento"),
            Issues.Create(3, "Bring project under UI test", "acme/memento"),
            Issues.Create(159, "Creating Galleries", "acme/gallery", labels: ["enhancement"]),
        ];

        _sidebar = new IssueSidebarViewModel(_gitHub, _tracker, _finder, new WorkspaceIssues(_gitHub), new FixedTimeProvider(DateTimeOffset.Parse("2025-03-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture)));
    }

    [TearDown]
    public void DisposeSidebar() => _sidebar.Dispose();

    private Task Refresh() => _sidebar.RefreshCommand.ExecuteAsync(null);

    [Test]
    public async Task ShowsTheErrorAndStopsLoadingWhenGitHubTimesOutWithoutTheUserCancelling()
    {
        _gitHub.FailWith = new TaskCanceledException("The request timed out.");

        await Refresh();

        Assert.That(_sidebar.ErrorMessage, Is.EqualTo("The request timed out."));
        Assert.That(_sidebar.IsLoading, Is.False);
    }

    [Test]
    public async Task ListsAnIssueOnceWhenPagingReturnsItTwice()
    {
        _gitHub.Issues = [.. _gitHub.Issues, _gitHub.Issues[0]];

        await Refresh();

        Assert.That(_sidebar.ErrorMessage, Is.Null);
        Assert.That(_sidebar.Groups.SelectMany(group => group.Items).Count(item => item.Issue.Number == 4), Is.EqualTo(1));
    }

    [Test]
    public async Task GroupsOpenIssuesByRepository()
    {
        await Refresh();

        Assert.That(_sidebar.Groups.Select(group => group.Name), Is.EqualTo(Expect.Seq("acme/gallery", "acme/memento")));
        Assert.That(_sidebar.Groups.Single(group => group.Name == "acme/memento").Items, Has.Count.EqualTo(2));
        Assert.That(_sidebar.CountText, Is.EqualTo("3 open issues"));
        Assert.That(_sidebar.IsLoading, Is.False);
    }

    [Test]
    public async Task StartsEveryIssueAsNotStarted()
    {
        await Refresh();

        Assert.That(_sidebar.Groups.SelectMany(group => group.Items).Select(item => item.Status), Has.All.EqualTo(IssueWorkStatus.NotStarted));
    }

    [Test]
    public async Task ShowsAStatusFromTheTrackerForIssuesAlreadyInProgress()
    {
        _tracker.Report(new IssueKey("acme/memento", 4), new IssueWorkState(IssueWorkStatus.Running));

        await Refresh();

        var item = _sidebar.Groups.SelectMany(group => group.Items).Single(candidate => candidate.Issue.Number == 4);
        Assert.That(item.Status, Is.EqualTo(IssueWorkStatus.Running));
        Assert.That(item.StatusText, Is.EqualTo("Running"));
    }

    [Test]
    public async Task UpdatesAnItemLiveWhenTheTrackerReportsAChange()
    {
        await Refresh();
        var item = _sidebar.Groups.SelectMany(group => group.Items).Single(candidate => candidate.Issue.Number == 159);
        var changed = new List<string?>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _tracker.Report(new IssueKey("acme/gallery", 159), new IssueWorkState(IssueWorkStatus.NeedsInput));

        Assert.That(item.Status, Is.EqualTo(IssueWorkStatus.NeedsInput));
        Assert.That(changed, Does.Contain(nameof(IssueItemViewModel.Status)));
        Assert.That(changed, Does.Contain(nameof(IssueItemViewModel.StatusText)));
    }

    [Test]
    public async Task IgnoresTrackerChangesForIssuesThatAreNotListed()
    {
        await Refresh();

        Assert.That(() => _tracker.Report(new IssueKey("acme/unknown", 1), new IssueWorkState(IssueWorkStatus.Done)), Throws.Nothing);
    }

    [Test]
    public async Task FiltersWithTheSearchTextAndShowsHowManyMatch()
    {
        await Refresh();

        _sidebar.SearchText = "galleries";

        Assert.That(_sidebar.Groups.Select(group => group.Name), Is.EqualTo(Expect.Seq("acme/gallery")));
        Assert.That(_sidebar.CountText, Is.EqualTo("1 of 3 open issues"));
    }

    [Test]
    public async Task ShowsEverythingAgainWhenTheSearchIsCleared()
    {
        await Refresh();
        _sidebar.SearchText = "galleries";

        _sidebar.SearchText = string.Empty;

        Assert.That(_sidebar.Groups, Has.Count.EqualTo(2));
        Assert.That(_sidebar.CountText, Is.EqualTo("3 open issues"));
    }

    [Test]
    public async Task RemembersWhichGroupTheUserCollapsedAcrossSearches()
    {
        await Refresh();
        _sidebar.Groups.Single(group => group.Name == "acme/memento").IsExpanded = false;

        _sidebar.SearchText = "notifications";
        _sidebar.SearchText = string.Empty;

        Assert.That(_sidebar.Groups.Single(group => group.Name == "acme/memento").IsExpanded, Is.False);
    }

    [Test]
    public async Task MarksWorktreesFromEarlierRunsAsReady()
    {
        _finder.Found.Add(new ExistingWorkspace(new RepositoryRef("acme", "memento"), 4, new WorktreeInfo(@"C:\wt\4", "kontext/4-add-notifications", "abc", IsMain: false)));

        await Refresh();

        var item = _sidebar.Groups.SelectMany(group => group.Items).Single(candidate => candidate.Issue.Number == 4);
        Assert.That(item.Status, Is.EqualTo(IssueWorkStatus.Ready));
        Assert.That(item.State.BranchName, Is.EqualTo("kontext/4-add-notifications"));
        Assert.That(item.State.WorktreePath, Is.EqualTo(@"C:\wt\4"));
    }

    [Test]
    public async Task DoesNotDowngradeAnIssueThatIsAlreadyRunning()
    {
        _tracker.Report(new IssueKey("acme/memento", 4), new IssueWorkState(IssueWorkStatus.Running));
        _finder.Found.Add(new ExistingWorkspace(new RepositoryRef("acme", "memento"), 4, new WorktreeInfo(@"C:\wt\4", "kontext/4-x", "abc", IsMain: false)));

        await Refresh();

        Assert.That(_tracker.StateOf(new IssueKey("acme/memento", 4)).Status, Is.EqualTo(IssueWorkStatus.Running));
    }

    private static ExistingWorkspace Workspace(int number, string repository = "acme/memento")
    {
        var parts = repository.Split('/');
        return new ExistingWorkspace(new RepositoryRef(parts[0], parts[1]), number, new WorktreeInfo($@"C:\wt\{number}", $"kontext/{number}-x", "abc", IsMain: false));
    }

    private static GitHubIssue Closed(int number, string title = "Old work", string repository = "acme/memento")
        => Issues.Create(number, title, repository) with { State = "closed" };

    [Test]
    public async Task IgnoresAWorktreeWhoseIssueGitHubCannotProvide()
    {
        _finder.Found.Add(Workspace(99));

        await Refresh();

        Assert.That(_tracker.StateOf(new IssueKey("acme/memento", 99)).Status, Is.EqualTo(IssueWorkStatus.NotStarted));
        Assert.That(_sidebar.Groups.SelectMany(group => group.Items).Any(item => item.Issue.Number == 99), Is.False);
    }

    [Test]
    public async Task KeepsAClosedIssueListedAsDoneWhileItsWorktreeRemains()
    {
        _gitHub.ClosedIssues.Add(Closed(7));
        _finder.Found.Add(Workspace(7));

        await Refresh();

        var item = _sidebar.Groups.SelectMany(group => group.Items).Single(candidate => candidate.Issue.Number == 7);
        Assert.That(item.Status, Is.EqualTo(IssueWorkStatus.Done));
        Assert.That(item.State.WorktreePath, Is.EqualTo(@"C:\wt\7"));
        Assert.That(_sidebar.CountText, Is.EqualTo("3 open issues · 1 closed"));
    }

    [Test]
    public async Task ListsAClosedIssueInItsOwnRepositoryGroup()
    {
        _gitHub.ClosedIssues.Add(Closed(7));
        _finder.Found.Add(Workspace(7));

        await Refresh();

        Assert.That(_sidebar.Groups.Single(group => group.Name == "acme/memento").Items.Select(item => item.Issue.Number), Does.Contain(7));
    }

    [Test]
    public async Task DropsAClosedIssueOnceItsWorktreeIsGone()
    {
        _gitHub.ClosedIssues.Add(Closed(7));
        _finder.Found.Add(Workspace(7));
        await Refresh();

        _finder.Found.Clear();
        await Refresh();

        Assert.That(_sidebar.Groups.SelectMany(group => group.Items).Any(item => item.Issue.Number == 7), Is.False);
        Assert.That(_sidebar.CountText, Is.EqualTo("3 open issues"));
    }

    [Test]
    public async Task DropsAClosedIssueAndItsSelectionTheMomentItsSessionIsCleared()
    {
        _gitHub.ClosedIssues.Add(Closed(7));
        _finder.Found.Add(Workspace(7));
        await Refresh();
        var key = new IssueKey("acme/memento", 7);
        _sidebar.SelectedItem = _sidebar.Groups.SelectMany(group => group.Items).Single(candidate => candidate.Issue.Number == 7);

        _tracker.Report(key, IssueWorkState.NotStarted);

        Assert.That(_sidebar.Groups.SelectMany(group => group.Items).Any(item => item.Issue.Number == 7), Is.False);
        Assert.That(_sidebar.SelectedItem, Is.Null);
        Assert.That(_sidebar.CountText, Is.EqualTo("3 open issues"));
    }

    [Test]
    public async Task KeepsAnOpenIssueListedWhenItsSessionIsCleared()
    {
        await Refresh();
        var key = new IssueKey("acme/memento", 4);
        _tracker.Report(key, new IssueWorkState(IssueWorkStatus.Ready, "features/4-x", @"C:\wt\4"));

        _tracker.Report(key, IssueWorkState.NotStarted);

        Assert.That(_sidebar.Groups.SelectMany(group => group.Items).Any(item => item.Issue.Number == 4), Is.True);
    }

    [Test]
    public async Task NeverListsAClosedIssueThatHasNoWorktree()
    {
        _gitHub.ClosedIssues.Add(Closed(7));

        await Refresh();

        Assert.That(_sidebar.Groups.SelectMany(group => group.Items).Any(item => item.Issue.Number == 7), Is.False);
    }

    [Test]
    public async Task TurnsAReadyIssueIntoDoneWhenItWasClosedMeanwhile()
    {
        _gitHub.ClosedIssues.Add(Closed(7));
        _finder.Found.Add(Workspace(7));
        _tracker.Report(new IssueKey("acme/memento", 7), new IssueWorkState(IssueWorkStatus.Ready, "kontext/7-x", @"C:\wt\7"));

        await Refresh();

        Assert.That(_tracker.StateOf(new IssueKey("acme/memento", 7)).Status, Is.EqualTo(IssueWorkStatus.Done));
    }

    [TestCase(IssueWorkStatus.Running)]
    [TestCase(IssueWorkStatus.NeedsInput)]
    public async Task LeavesAClosedIssueWithALiveSessionToWhatTheSessionReports(IssueWorkStatus live)
    {
        _gitHub.ClosedIssues.Add(Closed(7));
        _finder.Found.Add(Workspace(7));
        _tracker.Report(new IssueKey("acme/memento", 7), new IssueWorkState(live));

        await Refresh();

        Assert.That(_tracker.StateOf(new IssueKey("acme/memento", 7)).Status, Is.EqualTo(live));
    }

    [Test]
    public async Task ShowsAnOpenIssueMissingFromTheCappedListWhenItHasAWorktree()
    {
        _gitHub.ClosedIssues.Add(Issues.Create(500, "Beyond the cap", "acme/memento"));
        _finder.Found.Add(Workspace(500));

        await Refresh();

        var item = _sidebar.Groups.SelectMany(group => group.Items).Single(candidate => candidate.Issue.Number == 500);
        Assert.That(item.Status, Is.EqualTo(IssueWorkStatus.Ready), "it is open, so it is ready, not done");
    }

    [Test]
    public async Task KeepsTheSameIssueSelectedAcrossARefreshPointingAtTheReloadedItem()
    {
        await Refresh();
        var before = _sidebar.Groups.SelectMany(group => group.Items).Single(candidate => candidate.Issue.Number == 4);
        _sidebar.SelectedItem = before;

        await Refresh();

        Assert.That(_sidebar.SelectedItem, Is.Not.Null);
        Assert.That(_sidebar.SelectedItem!.Issue.Number, Is.EqualTo(4));
        Assert.That(_sidebar.SelectedItem, Is.Not.SameAs(before));
    }

    [Test]
    public async Task ClearsTheSelectionWhenTheSelectedIssueDisappears()
    {
        await Refresh();
        _sidebar.SelectedItem = _sidebar.Groups.SelectMany(group => group.Items).Single(candidate => candidate.Issue.Number == 4);

        _gitHub.Issues = [Issues.Create(159, "Creating Galleries", "acme/gallery")];
        await Refresh();

        Assert.That(_sidebar.SelectedItem, Is.Null);
    }

    [Test]
    public async Task ShowsTheErrorAndNoIssuesWhenLoadingFails()
    {
        _gitHub.FailWith = new TestKontextException("GitHub is down");

        await Refresh();

        Assert.That(_sidebar.ErrorMessage, Is.EqualTo("GitHub is down"));
        Assert.That(_sidebar.HasError, Is.True);
        Assert.That(_sidebar.Groups, Is.Empty);
        Assert.That(_sidebar.IsLoading, Is.False);
    }

    [Test]
    public async Task RecoversOnTheNextSuccessfulRefresh()
    {
        _gitHub.FailWith = new TestKontextException("GitHub is down");
        await Refresh();
        _gitHub.FailWith = null;

        await Refresh();

        Assert.That(_sidebar.HasError, Is.False);
        Assert.That(_sidebar.Groups, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ShowsTheEmptyStateWhenThereAreNoOpenIssues()
    {
        _gitHub.Issues = [];

        await Refresh();

        Assert.That(_sidebar.IsEmpty, Is.True);
        Assert.That(_sidebar.CountText, Is.Empty);
    }

    [Test]
    public async Task IgnoresAStaleLoadThatArrivesBeforeTheNewerOne()
    {
        _gitHub.Deferred = true;
        var older = Refresh();
        var newer = Refresh();
        Assert.That(_sidebar.IsLoading, Is.True);

        _gitHub.Complete(0, [Issues.Create(1, "From the older load", "acme/old")]);
        await older;
        Assert.That(_sidebar.Groups, Is.Empty, "the superseded result must not be shown");
        Assert.That(_sidebar.IsLoading, Is.True, "the newer load still owns the loading state");

        _gitHub.Complete(1, [Issues.Create(2, "From the newer load", "acme/new")]);
        await newer;

        Assert.That(_sidebar.Groups.Select(group => group.Name), Is.EqualTo(Expect.Seq("acme/new")));
        Assert.That(_sidebar.IsLoading, Is.False);
    }

    [Test]
    public async Task IgnoresAStaleLoadThatArrivesAfterTheNewerOne()
    {
        _gitHub.Deferred = true;
        var older = Refresh();
        var newer = Refresh();

        _gitHub.Complete(1, [Issues.Create(2, "From the newer load", "acme/new")]);
        await newer;

        _gitHub.Complete(0, [Issues.Create(1, "From the older load", "acme/old")]);
        await older;

        Assert.That(_sidebar.Groups.Select(group => group.Name), Is.EqualTo(Expect.Seq("acme/new")));
        Assert.That(_sidebar.IsLoading, Is.False);
    }

    [Test]
    public async Task ClearForgetsEverythingLoadedForThePreviousUser()
    {
        await Refresh();
        _sidebar.SelectedItem = _sidebar.Groups.First().Items.First();
        _sidebar.SearchText = "x";

        _sidebar.Clear();

        Assert.That(_sidebar.Groups, Is.Empty);
        Assert.That(_sidebar.SelectedItem, Is.Null);
        Assert.That(_sidebar.SearchText, Is.Empty);
        Assert.That(_sidebar.CountText, Is.Empty);
        Assert.That(_sidebar.HasError, Is.False);
    }

    [Test]
    public async Task StopsListeningToTheTrackerAfterDisposal()
    {
        await Refresh();
        var item = _sidebar.Groups.SelectMany(group => group.Items).First();
        _sidebar.Dispose();

        _tracker.Report(item.Key, new IssueWorkState(IssueWorkStatus.Done));

        Assert.That(item.Status, Is.EqualTo(IssueWorkStatus.NotStarted));
    }
}
