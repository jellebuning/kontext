using Kontext.Domain;

namespace Kontext.Presentation.UnitTests;

[TestFixture]
public class DisplayTextTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2025-06-15T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [TestCase(0, "just now")]
    [TestCase(59, "just now")]
    [TestCase(60, "1m ago")]
    [TestCase(59 * 60, "59m ago")]
    [TestCase(60 * 60, "1h ago")]
    [TestCase(23 * 3600, "23h ago")]
    [TestCase(24 * 3600, "1d ago")]
    [TestCase(29 * 24 * 3600, "29d ago")]
    public void FormatsRecentTimesRelativeToNow(int secondsAgo, string expected)
    {
        Assert.That(RelativeTime.Format(Now.AddSeconds(-secondsAgo), Now), Is.EqualTo(expected));
    }

    [Test]
    public void FallsBackToADateAfterAMonth()
    {
        var text = RelativeTime.Format(Now.AddDays(-45), Now);

        Assert.That(text, Does.Not.Contain("ago"));
        Assert.That(text, Does.Contain("2025"));
    }

    [Test]
    public void EveryStatusHasDistinctNonEmptyText()
    {
        var texts = Enum.GetValues<IssueWorkStatus>().Select(IssueStatusText.For).ToList();

        Assert.That(texts, Has.All.Not.Empty);
        Assert.That(texts, Is.Unique);
    }

    [TestCase(IssueWorkStatus.NotStarted, "Not started")]
    [TestCase(IssueWorkStatus.Ready, "Environment ready")]
    [TestCase(IssueWorkStatus.NeedsInput, "Needs input")]
    [TestCase(IssueWorkStatus.Done, "Done")]
    public void UsesTheWordsTheOwnerAskedFor(IssueWorkStatus status, string expected)
    {
        Assert.That(IssueStatusText.For(status), Is.EqualTo(expected));
    }
}
