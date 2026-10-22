using System.Text;
using Kontext.Contracts;

namespace Kontext.GitAutomation.Tests;

[TestFixture]
public class GitHubTokenAuthenticationTests
{
    private sealed class FakeTokens(string? token) : IGitHubTokenStore
    {
        public string? Load() => token;

        public void Save(string value) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();
    }

    [Test]
    public void SendsTheTokenOnlyToGitHubAndKeepsItOutOfTheCommandLine()
    {
        var environment = new GitHubTokenAuthentication(new FakeTokens("ghp_secret")).EnvironmentVariables();

        Assert.That(environment["GIT_CONFIG_KEY_0"], Is.EqualTo("http.https://github.com/.extraheader"));

        var header = environment["GIT_CONFIG_VALUE_0"];
        Assert.That(header, Does.StartWith("AUTHORIZATION: basic "));

        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["AUTHORIZATION: basic ".Length..]));
        Assert.That(decoded, Is.EqualTo("x-access-token:ghp_secret"));
    }

    [Test]
    public void SilencesCredentialHelpersSoNoSignInWindowCanAppear()
    {
        var environment = new GitHubTokenAuthentication(new FakeTokens("ghp_secret")).EnvironmentVariables();

        Assert.That(environment["GIT_CONFIG_COUNT"], Is.EqualTo("2"));
        Assert.That(environment["GIT_CONFIG_KEY_1"], Is.EqualTo("credential.helper"));
        Assert.That(environment["GIT_CONFIG_VALUE_1"], Is.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void ChangesNothingWhenThereIsNoToken(string? token)
    {
        Assert.That(new GitHubTokenAuthentication(new FakeTokens(token)).EnvironmentVariables(), Is.Empty);
    }
}
