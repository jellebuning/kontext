using System.Net;
using System.Text;
using Kontext.Domain;
using Kontext.TestSupport;

namespace Kontext.Claude.IntegrationTests;

[TestFixture]
public class LoopbackClaudeHookServerTests
{
    private LoopbackClaudeHookServer _server = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void CreateServer()
    {
        _server = new LoopbackClaudeHookServer();
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    [TearDown]
    public void DisposeServer()
    {
        _client.Dispose();
        _server.Dispose();
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static TaskCompletionSource<ClaudeHookEvent> Capture(LoopbackClaudeHookServer server)
    {
        var received = new TaskCompletionSource<ClaudeHookEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.EventReceived += (_, e) => received.TrySetResult(e.Hook);
        return received;
    }

    [Test]
    public void ListensOnTheLoopbackAddressWithoutAdministratorRights()
    {
        var address = _server.Start();

        Assert.That(address, Is.Not.Null);
        Assert.That(address!.Host, Is.EqualTo("127.0.0.1"));
        Assert.That(address.Port, Is.GreaterThan(1024));
    }

    [Test]
    public void StartingAgainReturnsTheSameAddress()
    {
        var first = _server.Start();

        Assert.That(_server.Start(), Is.EqualTo(first));
    }

    [Test]
    public async Task ReportsAnEventForTheSessionNamedInTheAddress()
    {
        var address = _server.Start()!;
        var received = Capture(_server);
        var session = TerminalSessionId.New();

        var response = await _client.PostAsync(new Uri(address, $"hook/{session.Value:D}"), Json("""{ "hook_event_name": "Stop", "session_id": "whatever-claude-calls-it" }"""));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var hook = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(hook, Is.EqualTo(new ClaudeHookEvent(session, "Stop")));
    }

    [Test]
    public async Task PassesTheNotificationTypeAlong()
    {
        var address = _server.Start()!;
        var received = Capture(_server);
        var session = TerminalSessionId.New();

        await _client.PostAsync(new Uri(address, $"hook/{session.Value:D}"), Json("""{ "hook_event_name": "Notification", "notification_type": "permission_prompt" }"""));

        Assert.That((await received.Task.WaitAsync(TimeSpan.FromSeconds(5))).NotificationType, Is.EqualTo("permission_prompt"));
    }

    [Test]
    public async Task ReportsEventsInTheOrderTheyArrive()
    {
        var address = _server.Start()!;
        var names = new List<string>();
        var all = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _server.EventReceived += (_, e) =>
        {
            lock (names)
            {
                names.Add(e.Hook.Name);
                if (names.Count == 4)
                {
                    all.TrySetResult();
                }
            }
        };
        var url = new Uri(address, $"hook/{Guid.NewGuid():D}");

        foreach (var name in new[] { "UserPromptSubmit", "PreToolUse", "PostToolUse", "Stop" })
        {
            await _client.PostAsync(url, Json($$"""{ "hook_event_name": "{{name}}" }"""));
        }

        await all.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(names, Is.EqualTo(Expect.Seq("UserPromptSubmit", "PreToolUse", "PostToolUse", "Stop")));
    }

    [Test]
    public async Task AnswersAtOnceEvenWhenNobodyIsListeningForEvents()
    {
        var address = _server.Start()!;

        var response = await _client.PostAsync(new Uri(address, $"hook/{Guid.NewGuid():D}"), Json("""{ "hook_event_name": "Stop" }"""));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [TestCase("hook/not-a-guid")]
    [TestCase("elsewhere")]
    [TestCase("")]
    public async Task AnswersButReportsNothingForAddressesThatAreNotASession(string path)
    {
        var address = _server.Start()!;
        var raised = false;
        _server.EventReceived += (_, _) => raised = true;

        var response = await _client.PostAsync(new Uri(address, path), Json("""{ "hook_event_name": "Stop" }"""));
        await Task.Delay(100);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(raised, Is.False);
    }

    [Test]
    public async Task AnswersButReportsNothingForABodyThatIsNotAHookEvent()
    {
        var address = _server.Start()!;
        var raised = false;
        _server.EventReceived += (_, _) => raised = true;

        var response = await _client.PostAsync(new Uri(address, $"hook/{Guid.NewGuid():D}"), Json("not json"));
        await Task.Delay(100);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(raised, Is.False);
    }

    [Test]
    public void CanBeDisposedBeforeItEverStartedAndMoreThanOnce()
    {
        _server.Dispose();
        _server.Dispose();

        Assert.That(_server.Start, Throws.Nothing);
    }
}
