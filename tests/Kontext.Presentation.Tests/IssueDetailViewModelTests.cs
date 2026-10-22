using Kontext.Domain;
using Kontext.Kernel;
using Kontext.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace Kontext.Presentation.Tests;

[TestFixture]
public class IssueDetailViewModelTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2025-03-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private FakeCoordinator _coordinator = null!;
    private FakeSessionClearer _clearer = null!;
    private FakeConfirmation _confirmation = null!;
    private FakeWorkspaceOpener _opener = null!;
    private InMemoryIssueSessionRegistry _registry = null!;
    private InMemoryIssueWorkTracker _tracker = null!;
    private FakeTimeProvider _clock = null!;
    private SelectionAutoStart _autoStart = null!;
    private IssueDetailViewModel _detail = null!;

    [SetUp]
    public void CreateDetail()
    {
        _coordinator = new FakeCoordinator();
        _clearer = new FakeSessionClearer();
        _confirmation = new FakeConfirmation();
        _opener = new FakeWorkspaceOpener();
        _registry = new InMemoryIssueSessionRegistry();
        _tracker = new InMemoryIssueWorkTracker();
        _clock = new FakeTimeProvider();
        _autoStart = new SelectionAutoStart(_coordinator, _tracker, new InMemoryIssueSessionRegistry(), _clock, new AutoStartOptions(TimeSpan.FromSeconds(1)));
        _detail = new IssueDetailViewModel(_coordinator, _clearer, _confirmation, _opener, _registry, _autoStart);
    }

    [TearDown]
    public void Dispose()
    {
        _detail.Dispose();
        _autoStart.Dispose();
    }

    private static IssueItemViewModel Item(IssueWorkStatus status = IssueWorkStatus.NotStarted, GitHubIssue? issue = null, string? branch = null, string? path = null, string? message = null, bool sessionEnded = false)
        => new(issue ?? Issues.Create(4, "Add notifications", "acme/memento", labels: ["enhancement", "ui"]), new IssueWorkState(status, branch, path, message, sessionEnded), Now);

    [Test]
    public void ShowsNothingUntilAnIssueIsSelected()
    {
        Assert.That(_detail.HasSelection, Is.False);
        Assert.That(_detail.HasNoSelection, Is.True);
        Assert.That(_detail.Title, Is.Empty);
        Assert.That(_detail.Session, Is.Null);
        Assert.That(_detail.ResumeCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void ExposesTheHeaderWithTheIssueNumberAsALinkTarget()
    {
        _detail.Item = Item();

        Assert.That(_detail.HasSelection, Is.True);
        Assert.That(_detail.Title, Is.EqualTo("Add notifications"));
        Assert.That(_detail.RepositoryName, Is.EqualTo("acme/memento"));
        Assert.That(_detail.NumberText, Is.EqualTo("#4"));
        Assert.That(_detail.Url, Is.EqualTo(new Uri("https://github.com/acme/memento/issues/4")));
        Assert.That(_detail.LabelsText, Is.EqualTo("enhancement · ui"));
        Assert.That(_detail.HasLabels, Is.True);
        Assert.That(_detail.StatusText, Is.EqualTo("Not started"));
    }

    [Test]
    public void ExposesTheDescriptionForTheIssueTab()
    {
        _detail.Item = Item();

        Assert.That(_detail.Body, Is.EqualTo("A body"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void ShowsAPlaceholderWhenTheIssueHasNoDescription(string? body)
    {
        _detail.Item = Item(issue: Issues.Create(body: body));

        Assert.That(_detail.Body, Does.Contain("No description"));
    }

    [Test]
    public void ShowsTheWorkspaceOnceOneExists()
    {
        _detail.Item = Item(IssueWorkStatus.Ready, branch: "kontext/4-x", path: @"C:\wt\4-x");

        Assert.That(_detail.HasWorkspace, Is.True);
        Assert.That(_detail.BranchName, Is.EqualTo("kontext/4-x"));
        Assert.That(_detail.WorktreePath, Is.EqualTo(@"C:\wt\4-x"));
    }

    [Test]
    public void HidesTheWorkspaceBeforeOneExists()
    {
        _detail.Item = Item();

        Assert.That(_detail.HasWorkspace, Is.False);
    }

    [Test]
    public void HasNoSessionUntilOneIsRegistered()
    {
        _detail.Item = Item();

        Assert.That(_detail.HasSession, Is.False);
        Assert.That(_detail.HasNoSession, Is.True);
        Assert.That(_detail.VisibleSession, Is.Null);
    }

    [Test]
    public void ShowsTheSessionTheMomentItIsRegisteredForTheSelectedIssue()
    {
        var item = Item();
        _detail.Item = item;
        var changed = new List<string?>();
        _detail.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var session = new FakeTerminalSession(TerminalSessionId.New());

        _registry.Register(item.Key, session);

        Assert.That(_detail.Session, Is.SameAs(session));
        Assert.That(_detail.HasSession, Is.True);
        Assert.That(changed, Does.Contain(string.Empty), "an empty name tells bindings that every property changed");
    }

    [Test]
    public void IgnoresSessionsRegisteredForOtherIssues()
    {
        _detail.Item = Item();
        var other = new FakeTerminalSession(TerminalSessionId.New());

        _registry.Register(new IssueKey("acme/memento", 99), other);

        Assert.That(_detail.HasSession, Is.False);
    }

    [Test]
    public void ShowsTheSessionOfWhicheverIssueIsSelected()
    {
        var first = Item();
        var second = Item(issue: Issues.Create(9, "Other", "acme/memento"));
        var firstSession = new FakeTerminalSession(TerminalSessionId.New());
        _registry.Register(first.Key, firstSession);

        _detail.Item = first;
        Assert.That(_detail.Session, Is.SameAs(firstSession));

        _detail.Item = second;
        Assert.That(_detail.Session, Is.Null);

        _detail.Item = first;
        Assert.That(_detail.Session, Is.SameAs(firstSession), "switching back finds the same live session");
    }

    [Test]
    public void ShowsTheTerminalTabByDefaultAndHidesTheTerminalWhileTheIssueTabIsShown()
    {
        var item = Item();
        _detail.Item = item;
        var session = new FakeTerminalSession(TerminalSessionId.New());
        _registry.Register(item.Key, session);

        Assert.That(_detail.ShowTerminalTab, Is.True);
        Assert.That(_detail.ShowIssueTab, Is.False);
        Assert.That(_detail.VisibleSession, Is.SameAs(session));

        _detail.ShowIssueTab = true;

        Assert.That(_detail.ShowTerminalTab, Is.False);
        Assert.That(_detail.VisibleSession, Is.Null, "the native terminal ignores an ancestor being collapsed, so it must be told to hide");
        Assert.That(_detail.Session, Is.SameAs(session), "the session itself is untouched");
    }

    [Test]
    public void AnnouncesTheTerminalVisibilityChangeWhenTheTabChanges()
    {
        var changed = new List<string?>();
        _detail.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _detail.ShowIssueTab = true;

        Assert.That(changed, Does.Contain(nameof(IssueDetailViewModel.VisibleSession)));
        Assert.That(changed, Does.Contain(nameof(IssueDetailViewModel.ShowTerminalTab)));
    }

    [Test]
    public void IsPendingWhileTheSelectionRestsBeforeClaudeStarts()
    {
        var item = Item();
        _detail.Item = item;

        _autoStart.OnSelected(item.Issue);

        Assert.That(_detail.IsPending, Is.True);
        Assert.That(_detail.TerminalPlaceholder, Is.EqualTo("Starting Claude in a moment…"));
    }

    [Test]
    public async Task StopsBeingPendingOnceTheStartBegins()
    {
        var item = Item();
        _detail.Item = item;
        _autoStart.OnSelected(item.Issue);

        _clock.Advance(TimeSpan.FromSeconds(1));

        await AsyncAssert.EventuallyAsync(() => !_detail.IsPending, "the pending note to clear");
    }

    [Test]
    public void IsNotPendingForADifferentIssueThanTheOneWaiting()
    {
        _detail.Item = Item();

        _autoStart.OnSelected(Issues.Create(99, "Elsewhere", "acme/memento"));

        Assert.That(_detail.IsPending, Is.False);
    }

    [TestCase(IssueWorkStatus.NotStarted, "Claude starts when you stay on this issue.")]
    [TestCase(IssueWorkStatus.Ready, "Claude starts when you stay on this issue.")]
    [TestCase(IssueWorkStatus.Preparing, "Preparing the workspace…")]
    public void ExplainsWhyThereIsNoTerminalYet(IssueWorkStatus status, string expected)
    {
        _detail.Item = Item(status);

        Assert.That(_detail.TerminalPlaceholder, Is.EqualTo(expected));
    }

    [Test]
    public void ExplainsAFailureThatHappenedBeforeAnyTerminalExisted()
    {
        _detail.Item = Item(IssueWorkStatus.Failed, message: "git exploded");

        Assert.That(_detail.TerminalPlaceholder, Is.EqualTo("git exploded"));
    }

    [Test]
    public void SaysTheIssueIsClosedInPlaceOfATerminalForAClosedIssueWithoutASession()
    {
        _detail.Item = Item(IssueWorkStatus.Done, branch: "kontext/7-x", path: @"C:\wt\7");

        Assert.That(_detail.TerminalPlaceholder, Is.EqualTo("This issue is closed."));
    }

    [TestCase(IssueWorkStatus.Done, true, true)]
    [TestCase(IssueWorkStatus.Ready, true, true)]
    [TestCase(IssueWorkStatus.Failed, true, true)]
    [TestCase(IssueWorkStatus.Failed, false, true)]
    [TestCase(IssueWorkStatus.Done, false, false)]
    [TestCase(IssueWorkStatus.Ready, false, false)]
    [TestCase(IssueWorkStatus.NotStarted, false, false)]
    [TestCase(IssueWorkStatus.Preparing, false, false)]
    [TestCase(IssueWorkStatus.Running, false, false)]
    [TestCase(IssueWorkStatus.NeedsInput, false, false)]
    public void OffersResumeOnlyOnceTheSessionHasEnded(IssueWorkStatus status, bool sessionEnded, bool offered)
    {
        _detail.Item = Item(status, sessionEnded: sessionEnded);

        Assert.That(_detail.HasEnded, Is.EqualTo(offered));
        Assert.That(_detail.ResumeCommand.CanExecute(null), Is.EqualTo(offered));
    }

    [Test]
    public void ExplainsHowTheSessionEnded()
    {
        _detail.Item = Item(IssueWorkStatus.Done, sessionEnded: true);
        Assert.That(_detail.EndedText, Is.EqualTo("Session ended."));

        _detail.Item = Item(IssueWorkStatus.Failed, message: "Claude exited with code 3.");
        Assert.That(_detail.EndedText, Is.EqualTo("Claude exited with code 3."));
    }

    [Test]
    public async Task ResumingAsksTheCoordinatorToStartTheSelectedIssueAgain()
    {
        var item = Item(IssueWorkStatus.Failed, message: "Claude exited with code 1.");
        _detail.Item = item;

        await _detail.ResumeCommand.ExecuteAsync(null);

        Assert.That(_coordinator.Started, Is.EqualTo(Expect.Seq(item.Issue)));
    }

    [Test]
    public void FollowsStatusChangesOfTheSelectedItemAndRefreshesWhatCanBeResumed()
    {
        var item = Item(IssueWorkStatus.Running);
        _detail.Item = item;
        Assert.That(_detail.ResumeCommand.CanExecute(null), Is.False);
        var changed = new List<string?>();
        _detail.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        item.State = new IssueWorkState(IssueWorkStatus.Done, SessionEnded: true);

        Assert.That(_detail.Status, Is.EqualTo(IssueWorkStatus.Done));
        Assert.That(_detail.StatusText, Is.EqualTo("Done"));
        Assert.That(_detail.HasEnded, Is.True);
        Assert.That(_detail.ResumeCommand.CanExecute(null), Is.True);
        Assert.That(changed, Does.Contain(string.Empty));
    }

    [Test]
    public void StopsFollowingAnItemOnceAnotherIsSelected()
    {
        var first = Item();
        var second = Item(issue: Issues.Create(9, "Other"));
        _detail.Item = first;
        _detail.Item = second;

        first.State = new IssueWorkState(IssueWorkStatus.Done);

        Assert.That(_detail.Title, Is.EqualTo("Other"));
        Assert.That(_detail.Status, Is.EqualTo(IssueWorkStatus.NotStarted));
    }

    [Test]
    public void ReturnsToTheEmptyStateWhenTheSelectionIsCleared()
    {
        _detail.Item = Item(IssueWorkStatus.Failed);

        _detail.Item = null;

        Assert.That(_detail.HasSelection, Is.False);
        Assert.That(_detail.ResumeCommand.CanExecute(null), Is.False);
        Assert.That(_detail.Session, Is.Null);
    }

    [Test]
    public void StopsListeningToTheRegistryAndAutoStartAfterDisposal()
    {
        var item = Item();
        _detail.Item = item;
        var changed = 0;
        _detail.PropertyChanged += (_, _) => changed++;
        _detail.Dispose();

        _registry.Register(item.Key, new FakeTerminalSession(TerminalSessionId.New()));
        _autoStart.OnSelected(item.Issue);

        Assert.That(changed, Is.Zero);
    }

    private static GitHubIssue ClosedIssue() => Issues.Create(4, "Add notifications", "acme/memento") with { State = "closed" };

    [Test]
    public void OffersToClearTheSessionOfAClosedIssueThatHasAWorkspace()
    {
        _detail.Item = Item(IssueWorkStatus.NeedsInput, ClosedIssue(), branch: "features/4-add", path: @"C:\wt\4");

        Assert.That(_detail.Status, Is.EqualTo(IssueWorkStatus.Done));
        Assert.That(_detail.CanClearSession, Is.True);
    }

    [TestCase(IssueWorkStatus.Running)]
    [TestCase(IssueWorkStatus.Preparing)]
    [TestCase(IssueWorkStatus.NotStarted)]
    public void DoesNotOfferToClearWhileAClosedIssueIsStillBeingWorkedOnOrHasNothingToClear(IssueWorkStatus status)
    {
        _detail.Item = Item(status, ClosedIssue(), branch: "features/4-add", path: @"C:\wt\4");

        Assert.That(_detail.CanClearSession, Is.False);
        Assert.That(_detail.ClearSessionCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void StillOffersToClearAClosedIssueWhoseWorkspaceCouldNotBeRemovedSoItCanBeRetried()
    {
        _detail.Item = Item(IssueWorkStatus.Failed, ClosedIssue(), branch: "features/4-add", path: @"C:\wt\4", message: "in use", sessionEnded: true);

        Assert.That(_detail.CanClearSession, Is.True);
    }

    [Test]
    public void DoesNotOfferToClearAnOpenIssue()
    {
        _detail.Item = Item(IssueWorkStatus.Ready, branch: "features/4-add", path: @"C:\wt\4");

        Assert.That(_detail.CanClearSession, Is.False);
    }

    [Test]
    public async Task AsksForConfirmationNamingTheWorktreeAndBranchBeforeClearing()
    {
        var item = Item(IssueWorkStatus.Done, ClosedIssue(), branch: "features/4-add", path: @"C:\wt\4");
        _detail.Item = item;

        await _detail.ClearSessionCommand.ExecuteAsync(null);

        var asked = _confirmation.Asked.Single();
        Assert.That(asked.Message, Does.Contain(@"C:\wt\4").And.Contain("features/4-add").And.Contain("unpushed commits are lost"));
        Assert.That(_clearer.Cleared, Is.EqualTo(Expect.Seq(item.Issue)));
    }

    [Test]
    public async Task ClearsNothingWhenTheUserCancels()
    {
        _confirmation.Answer = false;
        _detail.Item = Item(IssueWorkStatus.Done, ClosedIssue(), branch: "features/4-add", path: @"C:\wt\4");

        await _detail.ClearSessionCommand.ExecuteAsync(null);

        Assert.That(_clearer.Cleared, Is.Empty);
        Assert.That(_detail.IsConfirmingClear, Is.False);
    }

    [Test]
    public async Task HidesTheNativeTerminalWhileTheDialogIsOpenSoItIsNotCoveredByIt()
    {
        var item = Item(IssueWorkStatus.Done, ClosedIssue(), branch: "features/4-add", path: @"C:\wt\4");
        var session = new FakeTerminalSession(TerminalSessionId.New());
        _registry.Register(item.Key, session);
        _detail.Item = item;
        _confirmation.Deferred = true;
        Assert.That(_detail.VisibleSession, Is.SameAs(session));

        var clearing = _detail.ClearSessionCommand.ExecuteAsync(null);
        Assert.That(_detail.VisibleSession, Is.Null);

        _confirmation.Respond(false);
        await clearing;
        Assert.That(_detail.VisibleSession, Is.SameAs(session));
    }
    [TestCase(WorkspaceTool.Explorer)]
    [TestCase(WorkspaceTool.Terminal)]
    [TestCase(WorkspaceTool.VisualStudio)]
    public void OpensTheWorktreeInTheChosenTool(WorkspaceTool tool)
    {
        _detail.Item = Item(IssueWorkStatus.Ready, branch: "features/4-add", path: @"C:\wt\4");

        _detail.OpenInCommand.Execute(tool);

        Assert.That(_opener.Opened, Is.EqualTo(Expect.Seq((tool, @"C:\wt\4"))));
        Assert.That(_detail.HasToolError, Is.False);
    }

    [Test]
    public void OffersToOpenOnlyWhenThereIsAWorktree()
    {
        _detail.Item = Item(IssueWorkStatus.NotStarted);

        Assert.That(_detail.OpenInCommand.CanExecute(WorkspaceTool.Explorer), Is.False);

        _detail.Item = Item(IssueWorkStatus.Ready, branch: "features/4-add", path: @"C:\wt\4");

        Assert.That(_detail.OpenInCommand.CanExecute(WorkspaceTool.Explorer), Is.True);
    }

    [Test]
    public void ShowsWhyAToolCouldNotBeOpenedAndForgetsItOnceAnotherIssueIsSelected()
    {
        _opener.FailWith = new ExternalToolException("Visual Studio was not found on this computer.");
        _detail.Item = Item(IssueWorkStatus.Ready, branch: "features/4-add", path: @"C:\wt\4");

        _detail.OpenInCommand.Execute(WorkspaceTool.VisualStudio);

        Assert.That(_detail.ToolError, Is.EqualTo("Visual Studio was not found on this computer."));
        Assert.That(_detail.HasToolError, Is.True);

        _detail.Item = Item(IssueWorkStatus.Ready, branch: "features/4-add", path: @"C:\wt\4");

        Assert.That(_detail.HasToolError, Is.False);
    }
}
