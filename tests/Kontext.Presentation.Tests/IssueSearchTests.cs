using Kontext.Domain;
using Kontext.TestSupport;

namespace Kontext.Presentation.Tests;

[TestFixture]
public class IssueSearchTests
{
    private static readonly GitHubIssue Issue = Issues.Create(
        number: 158,
        title: "Refresh tokens expire too early",
        repository: "GalleryPixels/GalleryPixels",
        labels: ["bug", "help wanted"],
        author: "BrammyS");

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void MatchesEverythingWhenTheQueryIsEmpty(string? query)
    {
        Assert.That(IssueSearch.Matches(Issue, query), Is.True);
    }

    [TestCase("refresh")]
    [TestCase("TOKENS")]
    [TestCase("early")]
    public void MatchesTheTitleIgnoringCase(string query)
    {
        Assert.That(IssueSearch.Matches(Issue, query), Is.True);
    }

    [TestCase("gallerypixels")]
    [TestCase("pixels/gallery")]
    public void MatchesTheRepository(string query)
    {
        Assert.That(IssueSearch.Matches(Issue, query), Is.True);
    }

    [TestCase("bug")]
    [TestCase("wanted")]
    public void MatchesLabels(string query)
    {
        Assert.That(IssueSearch.Matches(Issue, query), Is.True);
    }

    [Test]
    public void MatchesTheAuthor()
    {
        Assert.That(IssueSearch.Matches(Issue, "brammy"), Is.True);
    }

    [TestCase("#158")]
    [TestCase("#15")]
    [TestCase("#1")]
    public void MatchesIssueNumbersByPrefixWhenPrefixedWithAHash(string query)
    {
        Assert.That(IssueSearch.Matches(Issue, query), Is.True);
    }

    [TestCase("#58")]
    [TestCase("#9")]
    public void DoesNotMatchNumbersThatMerelyContainTheDigits(string query)
    {
        Assert.That(IssueSearch.Matches(Issue, query), Is.False);
    }

    [Test]
    public void RequiresEveryWordToMatch()
    {
        Assert.That(IssueSearch.Matches(Issue, "refresh bug"), Is.True);
        Assert.That(IssueSearch.Matches(Issue, "refresh nonexistent"), Is.False);
    }

    [Test]
    public void DoesNotMatchUnrelatedText()
    {
        Assert.That(IssueSearch.Matches(Issue, "kubernetes"), Is.False);
    }

    [Test]
    public void ToleratesIssuesWithoutAnAuthor()
    {
        Assert.That(IssueSearch.Matches(Issues.Create(author: null), "someone"), Is.False);
    }
}
