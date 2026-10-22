using Kontext.Domain;
using Kontext.TestSupport;

namespace Kontext.Presentation.Tests;

[TestFixture]
public class IssueGrouperTests
{
    private static IReadOnlyList<IssueGroup<GitHubIssue>> Group(params GitHubIssue[] issues)
        => IssueGrouper.Group(issues, issue => issue.Repository.FullName, issue => issue.UpdatedAt);

    [Test]
    public void GroupsByRepositorySortedByNameIgnoringCase()
    {
        var groups = Group(
            Issues.Create(1, repository: "zeta/app"),
            Issues.Create(2, repository: "Alpha/app"),
            Issues.Create(3, repository: "zeta/app"));

        Assert.That(groups.Select(group => group.Repository), Is.EqualTo(Expect.Seq("Alpha/app", "zeta/app")));
        Assert.That(groups[1].Items, Has.Count.EqualTo(2));
    }

    [Test]
    public void OrdersEachGroupNewestFirst()
    {
        var old = DateTimeOffset.Parse("2025-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var recent = DateTimeOffset.Parse("2025-06-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

        var groups = Group(Issues.Create(1, updatedAt: old), Issues.Create(2, updatedAt: recent));

        Assert.That(groups.Single().Items.Select(issue => issue.Number), Is.EqualTo(Expect.Seq(2, 1)));
    }

    [Test]
    public void TreatsDifferentCasingsOfTheSameRepositoryAsOneGroup()
    {
        var groups = Group(Issues.Create(1, repository: "Acme/App"), Issues.Create(2, repository: "acme/app"));

        Assert.That(groups, Has.Count.EqualTo(1));
    }

    [Test]
    public void ReturnsNoGroupsForNoIssues()
    {
        Assert.That(Group(), Is.Empty);
    }
}
