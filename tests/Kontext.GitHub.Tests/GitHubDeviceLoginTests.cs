using System.Net;
using System.Text;
using Kontext.Domain;
using Microsoft.Extensions.Time.Testing;

namespace Kontext.GitHub.Tests;

[TestFixture]
public class GitHubDeviceLoginTests
{
    private const string ClientId = "Iv1.testclient";
    private const string Secret = "gho_supersecret";

    private const string DeviceCodeJson = """
        {
          "device_code": "device-123",
          "user_code": "WDJB-MJHT",
          "verification_uri": "https://github.com/login/device",
          "expires_in": 900,
          "interval": 5
        }
        """;

    private static readonly string GrantedJson = $$"""{ "access_token": "{{Secret}}", "token_type": "bearer", "scope": "repo" }""";
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private FakeTimeProvider _time = null!;
    private StubHandler _handler = null!;
    private HttpClient _http = null!;

    [SetUp]
    public void CreateClient()
    {
        _time = new FakeTimeProvider(DateTimeOffset.Parse("2025-03-01T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        _handler = new StubHandler();
        _http = new HttpClient(_handler);
    }

    [TearDown]
    public void DisposeClient()
    {
        _http.Dispose();
        _handler.Dispose();
    }

    private GitHubDeviceLogin Create(string? clientId = ClientId) => new(new GitHubOptions(clientId), _http, _time);

    private static DeviceCodeChallenge Challenge(DateTimeOffset expiresAt) =>
        new("device-123", "WDJB-MJHT", new Uri("https://github.com/login/device"), expiresAt, Interval);

    private DeviceCodeChallenge FreshChallenge() => Challenge(_time.GetUtcNow().AddMinutes(15));

    private async Task AdvanceUntilSettled(Task task, TimeSpan step)
    {
        for (var i = 0; i < 50 && !task.IsCompleted; i++)
        {
            var before = _handler.Requests.Count;
            _time.Advance(step);

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!task.IsCompleted && _handler.Requests.Count == before && DateTime.UtcNow < deadline)
            {
                await Task.Delay(1);
            }

            await Task.Delay(10);
        }
    }

    private static Task LetPendingWorkRun() => Task.Delay(20);

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task StartingWithoutAClientIdSaysSignInIsNotConfiguredAndMakesNoRequest(string? clientId)
    {
        var login = Create(clientId);

        var ex = await Assert.ThrowsAsync<GitHubException>(() => login.StartAsync());

        Assert.That(ex.Message, Does.Contain("not configured").And.Contain("KONTEXT_GITHUB_CLIENT_ID"));
        Assert.That(_handler.Requests, Is.Empty);
    }

    [TestCase(null)]
    [TestCase("  ")]
    public async Task WaitingWithoutAClientIdSaysSignInIsNotConfiguredAndMakesNoRequest(string? clientId)
    {
        var login = Create(clientId);

        var ex = await Assert.ThrowsAsync<GitHubException>(() => login.WaitForAccessTokenAsync(FreshChallenge()));

        Assert.That(ex.Message, Does.Contain("not configured"));
        Assert.That(_handler.Requests, Is.Empty);
    }

    [Test]
    public async Task StartPostsTheClientIdAndRepoScopeToTheDeviceCodeEndpointAsJson()
    {
        _handler.Enqueue(HttpStatusCode.OK, DeviceCodeJson);

        await Create().StartAsync();

        var request = _handler.Requests.Single();
        Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(request.Url, Is.EqualTo("https://github.com/login/device/code"));
        Assert.That(request.Accept, Does.Contain("application/json"));
        Assert.That(request.Form, Is.EquivalentTo(new Dictionary<string, string> { ["client_id"] = ClientId, ["scope"] = "repo" }));
    }

    [Test]
    public async Task StartReturnsTheChallengeWithTheExpiryRelativeToNow()
    {
        _handler.Enqueue(HttpStatusCode.OK, DeviceCodeJson);

        var challenge = await Create().StartAsync();

        Assert.That(challenge.DeviceCode, Is.EqualTo("device-123"));
        Assert.That(challenge.UserCode, Is.EqualTo("WDJB-MJHT"));
        Assert.That(challenge.VerificationUri, Is.EqualTo(new Uri("https://github.com/login/device")));
        Assert.That(challenge.PollInterval, Is.EqualTo(Interval));
        Assert.That(challenge.ExpiresAt, Is.EqualTo(_time.GetUtcNow().AddSeconds(900)));
    }

    [Test]
    public async Task StartMapsAConfigurationErrorFromGitHubToAGitHubException()
    {
        _handler.Enqueue(HttpStatusCode.OK, """{ "error": "device_flow_disabled", "error_description": "Device Flow must be explicitly enabled for this App" }""");

        var ex = await Assert.ThrowsAsync<GitHubException>(() => Create().StartAsync());

        Assert.That(ex.Message, Does.Contain("Device Flow must be explicitly enabled"));
    }

    [TestCase("http://github.com/login/device")]
    [TestCase("https://evil.example.com/login/device")]
    [TestCase("https://github.com.evil.example/login/device")]
    [TestCase("ftp://github.com/login/device")]
    public async Task StartRejectsAVerificationAddressThatIsNotHttpsOnGitHub(string verificationUri)
    {
        _handler.Enqueue(HttpStatusCode.OK, DeviceCodeJson.Replace("https://github.com/login/device", verificationUri, StringComparison.Ordinal));

        var ex = await Assert.ThrowsAsync<GitHubException>(() => Create().StartAsync());

        Assert.That(ex.Message, Does.Contain("unexpected verification address"));
    }

    [Test]
    public async Task StartMapsAnUnreachableGitHubToAGitHubException()
    {
        _handler.EnqueueFailure(new HttpRequestException("dns failure"));

        var ex = await Assert.ThrowsAsync<GitHubException>(() => Create().StartAsync());

        Assert.That(ex.Message, Does.Contain("Could not reach GitHub"));
        Assert.That(ex.InnerException, Is.InstanceOf<HttpRequestException>());
    }

    [Test]
    public async Task StartMapsANonJsonErrorBodyToAGitHubExceptionWithTheStatus()
    {
        _handler.Enqueue(HttpStatusCode.BadGateway, "<html>Bad gateway</html>");

        var ex = await Assert.ThrowsAsync<GitHubException>(() => Create().StartAsync());

        Assert.That(ex.Message, Does.Contain("502"));
    }

    [Test]
    public async Task StartMapsMalformedJsonToAGitHubException()
    {
        _handler.Enqueue(HttpStatusCode.OK, "this is not json");

        await Assert.ThrowsAsync<GitHubException>(() => Create().StartAsync());
    }

    [Test]
    public async Task StartHonoursAJsonErrorBodyOnANon2xxStatus()
    {
        _handler.Enqueue(HttpStatusCode.BadRequest, """{ "error": "incorrect_client_credentials" }""");

        var ex = await Assert.ThrowsAsync<GitHubException>(() => Create().StartAsync());

        Assert.That(ex.Message, Does.Contain("incorrect_client_credentials"));
    }

    [Test]
    public async Task WaitDoesNotPollBeforeTheIntervalHasPassed()
    {
        _handler.Enqueue(HttpStatusCode.OK, GrantedJson);

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge());
        await LetPendingWorkRun();
        _time.Advance(Interval - TimeSpan.FromSeconds(1));
        await LetPendingWorkRun();

        Assert.That(_handler.Requests, Is.Empty, "GitHub asks clients to wait the full interval");
        Assert.That(wait.IsCompleted, Is.False);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.That(await wait, Is.EqualTo(Secret));
        Assert.That(_handler.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task WaitPostsTheDeviceCodeAndGrantTypeToTheAccessTokenEndpointAsJson()
    {
        _handler.Enqueue(HttpStatusCode.OK, GrantedJson);

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge());
        await AdvanceUntilSettled(wait, Interval);
        await wait;

        var request = _handler.Requests.Single();
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
    public async Task WaitKeepsPollingWhileAuthorizationIsPendingThenReturnsTheToken()
    {
        _handler.Enqueue(HttpStatusCode.OK, """{ "error": "authorization_pending" }""");
        _handler.Enqueue(HttpStatusCode.OK, """{ "error": "authorization_pending" }""");
        _handler.Enqueue(HttpStatusCode.OK, GrantedJson);

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge());
        await AdvanceUntilSettled(wait, Interval);

        Assert.That(await wait, Is.EqualTo(Secret));
        Assert.That(_handler.Requests, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task SlowDownWithAnIntervalMakesTheNextPollWaitThatLong()
    {
        _handler.Enqueue(HttpStatusCode.OK, """{ "error": "slow_down", "interval": 12 }""");
        _handler.Enqueue(HttpStatusCode.OK, GrantedJson);

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge());
        await LetPendingWorkRun();
        _time.Advance(Interval);
        await WaitForRequests(1);

        await LetPendingWorkRun();
        _time.Advance(TimeSpan.FromSeconds(11));
        await LetPendingWorkRun();
        Assert.That(_handler.Requests, Has.Count.EqualTo(1), "the new interval of 12 seconds has not passed");

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.That(await wait, Is.EqualTo(Secret));
        Assert.That(_handler.Requests, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task SlowDownWithoutAnIntervalAddsFiveSeconds()
    {
        _handler.Enqueue(HttpStatusCode.OK, """{ "error": "slow_down" }""");
        _handler.Enqueue(HttpStatusCode.OK, GrantedJson);

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge());
        await LetPendingWorkRun();
        _time.Advance(Interval);
        await WaitForRequests(1);

        await LetPendingWorkRun();
        _time.Advance(TimeSpan.FromSeconds(9));
        await LetPendingWorkRun();
        Assert.That(_handler.Requests, Has.Count.EqualTo(1), "5 + 5 seconds have not passed");

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.That(await wait, Is.EqualTo(Secret));
        Assert.That(_handler.Requests, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task AnExpiredTokenResponseThrowsAnAuthenticationFailure()
    {
        _handler.Enqueue(HttpStatusCode.OK, """{ "error": "expired_token" }""");

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge());
        await AdvanceUntilSettled(wait, Interval);

        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() => wait);
        Assert.That(ex.Message, Is.EqualTo("The sign-in code expired. Please try again."));
    }

    [Test]
    public async Task ReachingTheExpiryTimeThrowsWithoutPollingAgain()
    {
        var wait = Create().WaitForAccessTokenAsync(Challenge(_time.GetUtcNow().AddSeconds(3)));
        await AdvanceUntilSettled(wait, Interval);

        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() => wait);
        Assert.That(ex.Message, Is.EqualTo("The sign-in code expired. Please try again."));
        Assert.That(_handler.Requests, Is.Empty);
    }

    [Test]
    public async Task ExpiryIsCheckedBetweenPollsSoAPendingCodeEventuallyExpires()
    {
        for (var i = 0; i < 10; i++)
        {
            _handler.Enqueue(HttpStatusCode.OK, """{ "error": "authorization_pending" }""");
        }

        var wait = Create().WaitForAccessTokenAsync(Challenge(_time.GetUtcNow().AddSeconds(12)));
        await AdvanceUntilSettled(wait, Interval);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => wait);
        Assert.That(_handler.Requests, Has.Count.EqualTo(2), "polls at 5 s and 10 s, then the 15 s tick is past the 12 s expiry");
    }

    [Test]
    public async Task AccessDeniedThrowsACancelledAuthenticationFailure()
    {
        _handler.Enqueue(HttpStatusCode.OK, """{ "error": "access_denied" }""");

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge());
        await AdvanceUntilSettled(wait, Interval);

        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() => wait);
        Assert.That(ex.Message, Is.EqualTo("Sign-in was cancelled on GitHub."));
    }

    [TestCase("device_flow_disabled", "Device Flow must be explicitly enabled for this App")]
    [TestCase("incorrect_client_credentials", null)]
    public async Task OtherErrorsThrowAGitHubExceptionWithAHint(string error, string? description)
    {
        _handler.Enqueue(HttpStatusCode.OK, description is null
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
        Assert.That(_handler.Requests, Is.Empty);
    }

    [Test]
    public async Task CancellingBetweenPollsStopsPolling()
    {
        using var cts = new CancellationTokenSource();
        _handler.Enqueue(HttpStatusCode.OK, """{ "error": "authorization_pending" }""");
        _handler.Enqueue(HttpStatusCode.OK, GrantedJson);

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge(), cts.Token);
        await LetPendingWorkRun();
        _time.Advance(Interval);
        await WaitForRequests(1);
        await cts.CancelAsync();
        _time.Advance(Interval);

        await Assert.CatchAsync<OperationCanceledException>(() => wait);
        Assert.That(_handler.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task WaitMapsAnUnreachableGitHubToAGitHubException()
    {
        _handler.EnqueueFailure(new HttpRequestException("connection reset"));

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge());
        await AdvanceUntilSettled(wait, Interval);

        var ex = await Assert.ThrowsAsync<GitHubException>(() => wait);
        Assert.That(ex.Message, Does.Contain("Could not reach GitHub"));
    }

    [Test]
    public async Task WaitMapsANonJsonErrorBodyToAGitHubExceptionWithTheStatus()
    {
        _handler.Enqueue(HttpStatusCode.ServiceUnavailable, "Service Unavailable");

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge());
        await AdvanceUntilSettled(wait, Interval);

        var ex = await Assert.ThrowsAsync<GitHubException>(() => wait);
        Assert.That(ex.Message, Does.Contain("503"));
    }

    [Test]
    public async Task WaitMapsMalformedJsonToAGitHubException()
    {
        _handler.Enqueue(HttpStatusCode.OK, "garbage");

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge());
        await AdvanceUntilSettled(wait, Interval);

        await Assert.ThrowsAsync<GitHubException>(() => wait);
    }

    [Test]
    public async Task EveryRequestOfAFullFlowGoesToGitHubDotCom()
    {
        _handler.Enqueue(HttpStatusCode.OK, DeviceCodeJson);
        _handler.Enqueue(HttpStatusCode.OK, """{ "error": "authorization_pending" }""");
        _handler.Enqueue(HttpStatusCode.OK, GrantedJson);
        var login = Create();

        var challenge = await login.StartAsync();
        var wait = login.WaitForAccessTokenAsync(challenge);
        await AdvanceUntilSettled(wait, Interval);
        await wait;

        Assert.That(_handler.Requests, Has.Count.EqualTo(3));
        Assert.That(_handler.Requests.Select(request => new Uri(request.Url)), Has.All.Matches<Uri>(uri => uri != null && uri.Scheme == Uri.UriSchemeHttps && uri.Host == "github.com"));
    }

    [TestCase(HttpStatusCode.OK)]
    [TestCase(HttpStatusCode.BadGateway)]
    public async Task ABodyThatCarriesTheTokenButCannotBeReadNeverEndsUpInTheExceptionMessage(HttpStatusCode status)
    {
        _handler.Enqueue(status, $"<html>{Secret}</html>");

        var wait = Create().WaitForAccessTokenAsync(FreshChallenge());
        await AdvanceUntilSettled(wait, Interval);

        var ex = await Assert.ThrowsAsync<GitHubException>(() => wait);
        Assert.That(ex.ToString(), Does.Not.Contain(Secret));
    }

    [Test]
    public async Task AGrantedResponseReturnsTheTokenAndAnErrorResponseNeverEchoesIt()
    {
        _handler.Enqueue(HttpStatusCode.OK, GrantedJson);
        _handler.Enqueue(HttpStatusCode.OK, """{ "error": "access_denied" }""");

        var granted = Create().WaitForAccessTokenAsync(FreshChallenge());
        await AdvanceUntilSettled(granted, Interval);
        var denied = Create().WaitForAccessTokenAsync(FreshChallenge());
        await AdvanceUntilSettled(denied, Interval);
        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() => denied);

        Assert.That(await granted, Is.EqualTo(Secret));
        Assert.That(ex.ToString(), Does.Not.Contain(Secret));
    }

    private async Task WaitForRequests(int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (_handler.Requests.Count < count)
        {
            Assert.That(DateTime.UtcNow, Is.LessThan(deadline), $"timed out waiting for {count} request(s)");
            await Task.Delay(5);
        }

        await LetPendingWorkRun();
    }

    private sealed record RecordedRequest(HttpMethod Method, string Url, string Accept, Dictionary<string, string> Form);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new();
        private readonly List<RecordedRequest> _requests = [];
        private readonly object _gate = new();

        public IReadOnlyList<RecordedRequest> Requests
        {
            get
            {
                lock (_gate)
                {
                    return [.. _requests];
                }
            }
        }

        public void Enqueue(HttpStatusCode status, string body) =>
            _responses.Enqueue(() => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });

        public void EnqueueFailure(Exception exception) => _responses.Enqueue(() => throw exception);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var form = content
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(parts => WebUtility.UrlDecode(parts[0]), parts => WebUtility.UrlDecode(parts.Length > 1 ? parts[1] : string.Empty));

            lock (_gate)
            {
                _requests.Add(new RecordedRequest(request.Method, request.RequestUri!.ToString(), request.Headers.Accept.ToString(), form));
            }

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No response was queued for this request.");
            }

            return _responses.Dequeue()();
        }
    }
}
