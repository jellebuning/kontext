namespace Kontext.Presentation.Tests;

[TestFixture]
public class LinkPolicyTests
{
    [TestCase("https://github.com/acme/app/issues/4")]
    [TestCase("http://example.com/page")]
    [TestCase("HTTPS://EXAMPLE.COM")]
    public void AllowsWebLinks(string link)
    {
        Assert.That(LinkPolicy.IsSafeToOpen(new Uri(link)), Is.True);
    }

    [TestCase("file:///C:/Windows/System32/calc.exe")]
    [TestCase("ms-settings:privacy")]
    [TestCase("vscode://file/C:/secrets.txt")]
    [TestCase("mailto:someone@example.com")]
    [TestCase("ftp://example.com/file")]
    public void RefusesEverythingThatIsNotAWebLink(string link)
    {
        Assert.That(LinkPolicy.IsSafeToOpen(new Uri(link)), Is.False);
    }

    [Test]
    public void RefusesNothingAtAllAndRelativeLinks()
    {
        Assert.That(LinkPolicy.IsSafeToOpen(null), Is.False);
        Assert.That(LinkPolicy.IsSafeToOpen(new Uri("/relative/path", UriKind.Relative)), Is.False);
    }
}
