using System.Globalization;
using Kontext.Domain;
using Microsoft.Extensions.Time.Testing;

namespace Kontext.GitHub.UnitTests;

public abstract class GitHubDeviceLoginTestBase
{
    protected const string ClientId = "Iv1.testclient";
    protected const string Secret = "gho_supersecret";

    protected const string DeviceCodeJson = """
        {
          "device_code": "device-123",
          "user_code": "WDJB-MJHT",
          "verification_uri": "https://github.com/login/device",
          "expires_in": 900,
          "interval": 5
        }
        """;

    protected static readonly string GrantedJson = $$"""{ "access_token": "{{Secret}}", "token_type": "bearer", "scope": "repo" }""";
    protected static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private HttpClient _http = null!;

    internal FakeTimeProvider Time { get; private set; } = null!;

    internal StubHandler Handler { get; private set; } = null!;

    [SetUp]
    public void CreateClient()
    {
        Time = new FakeTimeProvider(DateTimeOffset.Parse("2025-03-01T10:00:00Z", CultureInfo.InvariantCulture));
        Handler = new StubHandler();
        _http = new HttpClient(Handler);
    }

    [TearDown]
    public void DisposeClient()
    {
        _http.Dispose();
        Handler.Dispose();
    }

    internal GitHubDeviceLogin Create(string? clientId = ClientId) => new(new GitHubOptions(clientId), new GitHubFormPoster(_http), Time);

    protected static DeviceCodeChallenge Challenge(DateTimeOffset expiresAt) =>
        new("device-123", "WDJB-MJHT", new Uri("https://github.com/login/device"), expiresAt, Interval);

    protected DeviceCodeChallenge FreshChallenge() => Challenge(Time.GetUtcNow().AddMinutes(15));

    protected async Task AdvanceUntilSettled(Task task, TimeSpan step)
    {
        for (var i = 0; i < 50 && !task.IsCompleted; i++)
        {
            var before = Handler.Requests.Count;
            Time.Advance(step);

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!task.IsCompleted && Handler.Requests.Count == before && DateTime.UtcNow < deadline)
            {
                await Task.Delay(1);
            }

            await Task.Delay(10);
        }
    }

    protected static Task LetPendingWorkRun() => Task.Delay(20);

    protected async Task WaitForRequests(int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Handler.Requests.Count < count)
        {
            Assert.That(DateTime.UtcNow, Is.LessThan(deadline), $"timed out waiting for {count} request(s)");
            await Task.Delay(5);
        }

        await LetPendingWorkRun();
    }
}
