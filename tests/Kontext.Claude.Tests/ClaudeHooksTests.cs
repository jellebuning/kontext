using Kontext.Domain;
using Kontext.Kernel;
using Kontext.TestSupport;

namespace Kontext.Claude.Tests;

[TestFixture]
public class ClaudeHooksTests
{
    private sealed class FakeHookServer(Uri? address) : IClaudeHookServer
    {
        public event EventHandler<ClaudeHookEventArgs>? EventReceived;

        public int Starts { get; private set; }

        public Uri? Start()
        {
            Starts++;
            return address;
        }

        public void Raise(ClaudeHookEvent hook) => EventReceived?.Invoke(this, new ClaudeHookEventArgs(hook));
    }

    private static readonly IssueKey Key = new("acme/memento", 4);

    private InMemoryIssueWorkTracker _tracker = null!;
    private InMemoryIssueSessionRegistry _registry = null!;
    private FakeTerminalSession _session = null!;

    [SetUp]
    public void CreateFakes()
    {
        _tracker = new InMemoryIssueWorkTracker();
        _registry = new InMemoryIssueSessionRegistry();
        _session = new FakeTerminalSession(TerminalSessionId.New());
        _registry.Register(Key, _session);
        _tracker.Report(Key, new IssueWorkState(IssueWorkStatus.Ready));
    }

    private ClaudeHooks Create(FakeHookServer server)
        => new(server, new ClaudeActivityReporter(_registry, _tracker, new ImmediateDispatcher()));

    [Test]
    public void StartsListeningOnlyWhenAFirstSessionNeedsIt()
    {
        var server = new FakeHookServer(new Uri("http://127.0.0.1:50123/"));
        _ = Create(server);

        Assert.That(server.Starts, Is.Zero);
    }

    [Test]
    public void StartsListeningOnceNoMatterHowManySessionsAsk()
    {
        var server = new FakeHookServer(new Uri("http://127.0.0.1:50123/"));
        var hooks = Create(server);

        _ = hooks.SettingsFor(TerminalSessionId.New());
        _ = hooks.SettingsFor(TerminalSessionId.New());

        Assert.That(server.Starts, Is.EqualTo(1));
    }

    [Test]
    public void GivesASessionSettingsThatPostToTheServerWithItsOwnId()
    {
        var id = TerminalSessionId.New();

        var settings = Create(new FakeHookServer(new Uri("http://127.0.0.1:50123/"))).SettingsFor(id);

        Assert.That(settings, Does.Contain("http://127.0.0.1:50123/hook/" + id.Value.ToString("D")));
    }

    [Test]
    public void GivesNothingWhenTheServerCannotListenAndDoesNotKeepTrying()
    {
        var server = new FakeHookServer(null);
        var hooks = Create(server);

        Assert.That(hooks.SettingsFor(TerminalSessionId.New()), Is.Null);
        Assert.That(hooks.SettingsFor(TerminalSessionId.New()), Is.Null);
        Assert.That(server.Starts, Is.EqualTo(1));
    }

    [Test]
    public void TurnsWhatTheServerReceivesIntoStatusChanges()
    {
        var server = new FakeHookServer(new Uri("http://127.0.0.1:50123/"));
        _ = Create(server).SettingsFor(_session.Id);

        server.Raise(new ClaudeHookEvent(_session.Id, "UserPromptSubmit"));

        Assert.That(_tracker.StateOf(Key).Status, Is.EqualTo(IssueWorkStatus.Running));
    }
}
