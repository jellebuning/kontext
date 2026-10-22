namespace Kontext.GitAutomation.Tests;

[TestFixture]
public class BranchNamerTests
{
    private readonly BranchNamer _namer = new();

    [TestCase(123, "Fix login crash", "features/123-fix-login-crash")]
    [TestCase(5, "  Spaces   and --- dashes!! ", "features/5-spaces-and-dashes")]
    [TestCase(9, "Crème brûlée: déjà vu", "features/9-creme-brulee-deja-vu")]
    [TestCase(42, "Add Windows notifications (v2) #urgent", "features/42-add-windows-notifications-v2-urgent")]
    public void NamesBranchesFromNumberAndSlug(int number, string title, string expected)
    {
        Assert.That(_namer.Name(TestIssues.Create(number, title)), Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("日本語のタイトル")]
    [TestCase("!!!")]
    public void FallsBackToJustTheNumberWhenTheTitleHasNoUsableCharacters(string title)
    {
        Assert.That(_namer.Name(TestIssues.Create(77, title)), Is.EqualTo("features/77"));
    }

    [TestCase("features/123-fix-login", 123)]
    [TestCase("features/123", 123)]
    [TestCase("features/7-a", 7)]
    [TestCase("features/0-zero", 0)]
    public void ReadsTheIssueNumberBackFromABranchItNamed(string branch, int expected)
    {
        Assert.That(_namer.IssueNumberOf(branch), Is.EqualTo(expected));
    }

    [TestCase("kontext/123-fix-login", 123)]
    [TestCase("kontext/7", 7)]
    public void StillReadsTheNumberFromBranchesNamedBeforeTheFeaturesPrefix(string branch, int expected)
    {
        Assert.That(_namer.IssueNumberOf(branch), Is.EqualTo(expected));
    }

    [TestCase("main")]
    [TestCase("feature/123-fix")]
    [TestCase("features/")]
    [TestCase("features/fix-login")]
    [TestCase("features/123abc")]
    [TestCase("features/-5-x")]
    [TestCase("features/99999999999-overflow")]
    [TestCase("kontext/")]
    [TestCase("kontext/fix-login")]
    [TestCase("kontext/123abc")]
    [TestCase("")]
    public void IgnoresBranchesThatDoNotFollowTheScheme(string branch)
    {
        Assert.That(_namer.IssueNumberOf(branch), Is.Null);
    }

    [Test]
    public void RoundTripsEveryNameItProduces()
    {
        var issue = TestIssues.Create(2048, "Support café menus & more!");

        Assert.That(_namer.IssueNumberOf(_namer.Name(issue)), Is.EqualTo(2048));
    }

    [Test]
    public void TruncatesLongTitlesWithoutLeavingATrailingDash()
    {
        var name = _namer.Name(TestIssues.Create(1, "a very long title that keeps going and going and going well past the limit"));

        Assert.That(name, Does.StartWith("features/1-a-very-long-title"));
        Assert.That(name, Does.Not.EndWith("-"));
        Assert.That(name.Length, Is.LessThanOrEqualTo("features/1-".Length + 40));
    }
}
