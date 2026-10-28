using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;
using Kontext.TestSupport;

namespace Kontext.Sessions.UnitTests;

[TestFixture]
public class IssueWorkTrackerTests
{
    private IssueWorkTracker _tracker = null!;

    [SetUp]
    public void CreateTracker() => _tracker = new IssueWorkTracker();

    [Test]
    public void ReportsNotStartedForAnIssueNobodyReportedOn()
    {
        Assert.That(_tracker.StateOf(new IssueKey("acme/app", 1)).Status, Is.EqualTo(IssueWorkStatus.NotStarted));
    }

    [Test]
    public void ReturnsTheLatestReportedState()
    {
        var key = new IssueKey("acme/app", 1);

        _tracker.Report(key, new IssueWorkState(IssueWorkStatus.Preparing));
        _tracker.Report(key, new IssueWorkState(IssueWorkStatus.Ready, "kontext/1-x", @"C:\wt"));

        var state = _tracker.StateOf(key);
        Assert.That(state.Status, Is.EqualTo(IssueWorkStatus.Ready));
        Assert.That(state.BranchName, Is.EqualTo("kontext/1-x"));
        Assert.That(state.WorktreePath, Is.EqualTo(@"C:\wt"));
    }

    [Test]
    public void RaisesChangedWithTheIssueAndNewState()
    {
        var key = new IssueKey("acme/app", 7);
        IssueWorkChangedEventArgs? seen = null;
        _tracker.Changed += (_, e) => seen = e;

        _tracker.Report(key, new IssueWorkState(IssueWorkStatus.Running));

        Assert.That(seen?.Issue, Is.EqualTo(key));
        Assert.That(seen?.State.Status, Is.EqualTo(IssueWorkStatus.Running));
    }

    [Test]
    public void KeepsIssuesIndependent()
    {
        _tracker.Report(new IssueKey("acme/app", 1), new IssueWorkState(IssueWorkStatus.Done));

        Assert.That(_tracker.StateOf(new IssueKey("acme/app", 2)).Status, Is.EqualTo(IssueWorkStatus.NotStarted));
        Assert.That(_tracker.StateOf(new IssueKey("acme/other", 1)).Status, Is.EqualTo(IssueWorkStatus.NotStarted));
    }

    [Test]
    public void TreatsRepositoryNamesCaseInsensitively()
    {
        _tracker.Report(new IssueKey("Acme/App", 1), new IssueWorkState(IssueWorkStatus.Running));

        Assert.That(_tracker.StateOf(new IssueKey("acme/app", 1)).Status, Is.EqualTo(IssueWorkStatus.Running));
    }

    [Test]
    public void BuildsKeysFromIssuesAndRepositoriesConsistently()
    {
        var issue = Issues.Create(5, repository: "Acme/App");

        Assert.That(issue.Key, Is.EqualTo(new IssueKey("acme/app", 5)));
        Assert.That(new IssueKey(issue.Repository, 5), Is.EqualTo(issue.Key));
    }
}
