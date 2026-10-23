using System.Net;
using Kontext.Domain;

namespace Kontext.GitHub.Tests;

[TestFixture]
public class GitHubDeviceLoginWaitTests : GitHubDeviceLoginTestBase
{
    private const string Pending = """{ "error": "authorization_pending" }""";

    [TestCase(null)]
    [TestCase("  ")]
    public async Task WithoutAClientIdSaysSignInIsNotConfiguredAndMakesNoRequest(string? clientId)
    {
        var ex = await Assert.ThrowsAsync<GitHubException>(() => Create(clientId).WaitForAccessTokenAsync(FreshChallenge()));

        Assert.That(ex.Message, Does.Contain("not configured"));
        Assert.That(Handler.Requests, Is.Empty);
    }

    [Test]
    public async Task DoesNotPollBeforeTheIntervalHasPassed()
    {
        Handler.Enqueue(HttpStatusCode.OK, GrantedJson);

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge());
        await LetPendingWorkRun();
        Time.Advance(Interval - TimeSpan.FromSeconds(1));
        await LetPendingWorkRun();

        Assert.That(Handler.Requests, Is.Empty, "GitHub asks clients to wait the full interval");
        Assert.That(wait.IsCompleted, Is.False);

        Time.Advance(TimeSpan.FromSeconds(1));
        Assert.That(await wait, Is.EqualTo(Secret));
        Assert.That(Handler.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task PostsTheDeviceCodeAndGrantTypeToTheAccessTokenEndpointAsJson()
    {
        Handler.Enqueue(HttpStatusCode.OK, GrantedJson);

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge());
        await AdvanceUntilSettled(wait, Interval);
        await wait;

        var request = Handler.Requests.Single();
        Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(request.Url, Is.EqualTo("https://github.com/login/oauth/access_token"));
        Assert.That(request.Accept, Does.Contain("application/json"));
        Assert.That(
            request.Form,
            Is.EquivalentTo(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["device_code"] = "device-123",
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            }));
    }

    [Test]
    public async Task KeepsPollingWhileAuthorizationIsPendingThenReturnsTheToken()
    {
        Handler.Enqueue(HttpStatusCode.OK, Pending);
        Handler.Enqueue(HttpStatusCode.OK, Pending);
        Handler.Enqueue(HttpStatusCode.OK, GrantedJson);

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge());
        await AdvanceUntilSettled(wait, Interval);

        Assert.That(await wait, Is.EqualTo(Secret));
        Assert.That(Handler.Requests, Has.Count.EqualTo(3));
    }

    [TestCase("""{ "error": "slow_down", "interval": 12 }""", 11, TestName = "SlowDownWithAnIntervalMakesTheNextPollWaitThatLong")]
    [TestCase("""{ "error": "slow_down" }""", 9, TestName = "SlowDownWithoutAnIntervalAddsFiveSeconds")]
    public async Task SlowDownLengthensTheNextWait(string slowDown, int secondsStillTooEarly)
    {
        Handler.Enqueue(HttpStatusCode.OK, slowDown);
        Handler.Enqueue(HttpStatusCode.OK, GrantedJson);

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge());
        await LetPendingWorkRun();
        Time.Advance(Interval);
        await WaitForRequests(1);

        await LetPendingWorkRun();
        Time.Advance(TimeSpan.FromSeconds(secondsStillTooEarly));
        await LetPendingWorkRun();
        Assert.That(Handler.Requests, Has.Count.EqualTo(1), "the longer interval has not passed yet");

        Time.Advance(TimeSpan.FromSeconds(1));
        Assert.That(await wait, Is.EqualTo(Secret));
        Assert.That(Handler.Requests, Has.Count.EqualTo(2));
    }

    [TestCase("""{ "error": "expired_token" }""", "The sign-in code expired. Please try again.")]
    [TestCase("""{ "error": "access_denied" }""", "Sign-in was cancelled on GitHub.")]
    public async Task AnExpiredOrDeniedResponseThrowsAnAuthenticationFailure(string response, string message)
    {
        Handler.Enqueue(HttpStatusCode.OK, response);

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge());
        await AdvanceUntilSettled(wait, Interval);

        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() => wait);
        Assert.That(ex.Message, Is.EqualTo(message));
    }

    [Test]
    public async Task ReachingTheExpiryTimeThrowsWithoutPollingAgain()
    {
        var wait = Create().WaitForAccessTokenAsync(Challenge(Time.GetUtcNow().AddSeconds(3)));
        await AdvanceUntilSettled(wait, Interval);

        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() => wait);
        Assert.That(ex.Message, Is.EqualTo("The sign-in code expired. Please try again."));
        Assert.That(Handler.Requests, Is.Empty);
    }

    [Test]
    public async Task ExpiryIsCheckedBetweenPollsSoAPendingCodeEventuallyExpires()
    {
        for (var i = 0; i < 10; i++)
        {
            Handler.Enqueue(HttpStatusCode.OK, Pending);
        }

        var wait = Create().WaitForAccessTokenAsync(Challenge(Time.GetUtcNow().AddSeconds(12)));
        await AdvanceUntilSettled(wait, Interval);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => wait);
        Assert.That(Handler.Requests, Has.Count.EqualTo(2), "polls at 5 s and 10 s, then the 15 s tick is past the 12 s expiry");
    }

    [TestCase("device_flow_disabled", "Device Flow must be explicitly enabled for this App")]
    [TestCase("incorrect_client_credentials", null)]
    public async Task OtherErrorsThrowAGitHubExceptionWithAHint(string error, string? description)
    {
        Handler.Enqueue(HttpStatusCode.OK, description is null
            ? $$"""{ "error": "{{error}}" }"""
            : $$"""{ "error": "{{error}}", "error_description": "{{description}}" }""");

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge());
        await AdvanceUntilSettled(wait, Interval);

        var ex = await Assert.ThrowsAsync<GitHubException>(() => wait);
        Assert.That(ex.Message, Does.Contain(description ?? error));
        Assert.That(ex.Message, Does.Contain("device flow is enabled"));
    }

    [Test]
    public async Task CancellingWhileWaitingForTheIntervalStopsPollingWithoutARequest()
    {
        using var cts = new CancellationTokenSource();

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge(), cts.Token);
        await LetPendingWorkRun();
        await cts.CancelAsync();

        await Assert.CatchAsync<OperationCanceledException>(() => wait);
        Assert.That(Handler.Requests, Is.Empty);
    }

    [Test]
    public async Task CancellingBetweenPollsStopsPolling()
    {
        using var cts = new CancellationTokenSource();
        Handler.Enqueue(HttpStatusCode.OK, Pending);
        Handler.Enqueue(HttpStatusCode.OK, GrantedJson);

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge(), cts.Token);
        await LetPendingWorkRun();
        Time.Advance(Interval);
        await WaitForRequests(1);
        await cts.CancelAsync();
        Time.Advance(Interval);

        await Assert.CatchAsync<OperationCanceledException>(() => wait);
        Assert.That(Handler.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task EveryRequestOfAFullFlowGoesToGitHubDotCom()
    {
        Handler.Enqueue(HttpStatusCode.OK, DeviceCodeJson);
        Handler.Enqueue(HttpStatusCode.OK, Pending);
        Handler.Enqueue(HttpStatusCode.OK, GrantedJson);
        var login = Create();

        var challenge = await login.StartAsync();
        var wait = login.WaitForAccessTokenAsync(challenge);
        await AdvanceUntilSettled(wait, Interval);
        await wait;

        Assert.That(Handler.Requests, Has.Count.EqualTo(3));
        Assert.That(Handler.Requests.Select(request => new Uri(request.Url)), Has.All.Matches<Uri>(uri => uri != null && uri.Scheme == Uri.UriSchemeHttps && uri.Host == "github.com"));
    }

    [TestCase(HttpStatusCode.OK)]
    [TestCase(HttpStatusCode.BadGateway)]
    public async Task ABodyThatCarriesTheTokenButCannotBeReadNeverEndsUpInTheExceptionMessage(HttpStatusCode status)
    {
        Handler.Enqueue(status, $"<html>{Secret}</html>");

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge());
        await AdvanceUntilSettled(wait, Interval);

        var ex = await Assert.ThrowsAsync<GitHubException>(() => wait);
        Assert.That(ex.ToString(), Does.Not.Contain(Secret));
    }
}
