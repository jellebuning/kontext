using Kontext.Domain;
using Kontext.TestSupport;

namespace Kontext.Presentation.UnitTests;

[TestFixture]
public class IssueItemViewModelTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2025-03-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static IssueItemViewModel Item(IssueWorkStatus status, bool closed)
    {
        var issue = Issues.Create(4, "Add notifications", "acme/memento");
        return new IssueItemViewModel(closed ? issue with { State = "closed" } : issue, new IssueWorkState(status), Now);
    }

    [TestCase(IssueWorkStatus.NeedsInput)]
    [TestCase(IssueWorkStatus.Ready)]
    [TestCase(IssueWorkStatus.Done)]
    public void ShowsAClosedIssueAsDoneWhileNothingIsBeingWorkedOn(IssueWorkStatus status)
    {
        var item = Item(status, closed: true);

        Assert.That(item.Status, Is.EqualTo(IssueWorkStatus.Done));
        Assert.That(item.StatusText, Is.EqualTo("Done"));
    }

    [TestCase(IssueWorkStatus.Running)]
    [TestCase(IssueWorkStatus.Preparing)]
    [TestCase(IssueWorkStatus.Failed)]
    public void KeepsTheRealStatusOfAClosedIssueThatIsBusyOrBroken(IssueWorkStatus status)
    {
        Assert.That(Item(status, closed: true).Status, Is.EqualTo(status));
    }

    [TestCase(IssueWorkStatus.NeedsInput)]
    [TestCase(IssueWorkStatus.Ready)]
    public void KeepsTheRealStatusOfAnOpenIssue(IssueWorkStatus status)
    {
        Assert.That(Item(status, closed: false).Status, Is.EqualTo(status));
    }

    [Test]
    public void FollowsTheStateWhenItChanges()
    {
        var item = Item(IssueWorkStatus.NeedsInput, closed: true);
        var changed = new List<string?>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        item.State = new IssueWorkState(IssueWorkStatus.Running);

        Assert.That(item.Status, Is.EqualTo(IssueWorkStatus.Running));
        Assert.That(changed, Does.Contain(nameof(IssueItemViewModel.Status)));
    }
}
