namespace Kontext.GitAutomation.Tests;

[TestFixture]
public class GitRemoteUrlParserTests
{
    [TestCase("https://github.com/acme/app.git")]
    [TestCase("https://github.com/acme/app")]
    [TestCase("https://github.com/acme/app/")]
    [TestCase("https://user@github.com/acme/app.git")]
    [TestCase("http://www.github.com/acme/app.git")]
    [TestCase("git@github.com:acme/app.git")]
    [TestCase("git@github.com:acme/app")]
    [TestCase("ssh://git@github.com/acme/app.git")]
    [TestCase("ssh://git@github.com:22/acme/app.git")]
    [TestCase("git://github.com/acme/app.git")]
    public void RecognisesGitHubRemoteForms(string url)
    {
        var repository = GitRemoteUrlParser.Parse(url);

        Assert.That(repository?.FullName, Is.EqualTo("acme/app"));
    }

    [Test]
    public void KeepsDotsInsideRepositoryNames()
    {
        Assert.That(GitRemoteUrlParser.Parse("https://github.com/acme/my.app.git")?.Name, Is.EqualTo("my.app"));
    }

    [TestCase("https://gitlab.com/acme/app.git")]
    [TestCase("git@bitbucket.org:acme/app.git")]
    [TestCase("https://github.com/acme")]
    [TestCase("not a url")]
    [TestCase("")]
    public void IgnoresEverythingElse(string url)
    {
        Assert.That(GitRemoteUrlParser.Parse(url), Is.Null);
    }
}
