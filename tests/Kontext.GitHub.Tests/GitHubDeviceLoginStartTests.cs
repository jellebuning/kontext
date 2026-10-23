using System.Net;

namespace Kontext.GitHub.Tests;

[TestFixture]
public class GitHubDeviceLoginStartTests : GitHubDeviceLoginTestBase
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task WithoutAClientIdSaysSignInIsNotConfiguredAndMakesNoRequest(string? clientId)
    {
        var ex = await Assert.ThrowsAsync<GitHubException>(() => Create(clientId).StartAsync());

        Assert.That(ex.Message, Does.Contain("not configured").And.Contain("KONTEXT_GITHUB_CLIENT_ID"));
        Assert.That(Handler.Requests, Is.Empty);
    }

    [Test]
    public async Task PostsTheClientIdAndRepoScopeToTheDeviceCodeEndpointAsJson()
    {
        Handler.Enqueue(HttpStatusCode.OK, DeviceCodeJson);

        await Create().StartAsync();

        var request = Handler.Requests.Single();
        Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(request.Url, Is.EqualTo("https://github.com/login/device/code"));
        Assert.That(request.Accept, Does.Contain("application/json"));
        Assert.That(request.Form, Is.EquivalentTo(new Dictionary<string, string> { ["client_id"] = ClientId, ["scope"] = "repo" }));
    }

    [Test]
    public async Task ReturnsTheChallengeWithTheExpiryRelativeToNow()
    {
        Handler.Enqueue(HttpStatusCode.OK, DeviceCodeJson);

        var challenge = await Create().StartAsync();

        Assert.That(challenge.DeviceCode, Is.EqualTo("device-123"));
        Assert.That(challenge.UserCode, Is.EqualTo("WDJB-MJHT"));
        Assert.That(challenge.VerificationUri, Is.EqualTo(new Uri("https://github.com/login/device")));
        Assert.That(challenge.PollInterval, Is.EqualTo(Interval));
        Assert.That(challenge.ExpiresAt, Is.EqualTo(Time.GetUtcNow().AddSeconds(900)));
    }

    [TestCase("http://github.com/login/device")]
    [TestCase("https://evil.example.com/login/device")]
    [TestCase("https://github.com.evil.example/login/device")]
    [TestCase("ftp://github.com/login/device")]
    public async Task RejectsAVerificationAddressThatIsNotHttpsOnGitHub(string verificationUri)
    {
        Handler.Enqueue(HttpStatusCode.OK, DeviceCodeJson.Replace("https://github.com/login/device", verificationUri, StringComparison.Ordinal));

        var ex = await Assert.ThrowsAsync<GitHubException>(() => Create().StartAsync());

        Assert.That(ex.Message, Does.Contain("unexpected verification address"));
    }

    [Test]
    public async Task MapsAJsonErrorBodyOnANon2xxStatusToAGitHubException()
    {
        Handler.Enqueue(HttpStatusCode.BadRequest, """{ "error": "incorrect_client_credentials" }""");

        var ex = await Assert.ThrowsAsync<GitHubException>(() => Create().StartAsync());

        Assert.That(ex.Message, Does.Contain("incorrect_client_credentials"));
    }

    [Test]
    public async Task MapsAnUnreachableGitHubToAGitHubException()
    {
        Handler.EnqueueFailure(new HttpRequestException("dns failure"));

        var ex = await Assert.ThrowsAsync<GitHubException>(() => Create().StartAsync());

        Assert.That(ex.Message, Does.Contain("Could not reach GitHub"));
        Assert.That(ex.InnerException, Is.InstanceOf<HttpRequestException>());
    }

    [Test]
    public async Task MapsANonJsonErrorBodyToAGitHubExceptionWithTheStatus()
    {
        Handler.Enqueue(HttpStatusCode.BadGateway, "<html>Bad gateway</html>");

        var ex = await Assert.ThrowsAsync<GitHubException>(() => Create().StartAsync());

        Assert.That(ex.Message, Does.Contain("502"));
    }

    [Test]
    public async Task MapsMalformedJsonToAGitHubException()
    {
        Handler.Enqueue(HttpStatusCode.OK, "this is not json");

        await Assert.ThrowsAsync<GitHubException>(() => Create().StartAsync());
    }
}
