using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;
using Kontext.TestSupport;

namespace Kontext.Sessions.UnitTests;

[TestFixture]
public class IssueSessionRegistryTests
{
    private IssueSessionRegistry _registry = null!;

    [SetUp]
    public void CreateRegistry() => _registry = new IssueSessionRegistry();

    [Test]
    public void FindsNothingForAnIssueWithoutASession()
    {
        Assert.That(_registry.Find(new IssueKey("acme/app", 1)), Is.Null);
    }

    [Test]
    public void FindsTheSessionRegisteredForAnIssue()
    {
        var key = new IssueKey("acme/app", 1);
        var session = new FakeTerminalSession(TerminalSessionId.New());

        _registry.Register(key, session);

        Assert.That(_registry.Find(key), Is.SameAs(session));
        Assert.That(_registry.Find(new IssueKey("acme/app", 2)), Is.Null);
    }

    [Test]
    public void FindsTheIssueASessionBelongsTo()
    {
        var key = new IssueKey("acme/app", 1);
        var session = new FakeTerminalSession(TerminalSessionId.New());
        _registry.Register(new IssueKey("acme/app", 2), new FakeTerminalSession(TerminalSessionId.New()));
        _registry.Register(key, session);

        Assert.That(_registry.IssueOf(session.Id), Is.EqualTo(key));
    }

    [Test]
    public void FindsNoIssueForASessionItDoesNotKnow()
    {
        Assert.That(_registry.IssueOf(TerminalSessionId.New()), Is.Null);
    }

    [Test]
    public void ReplacesTheSessionWhenAnIssueIsResumed()
    {
        var key = new IssueKey("acme/app", 1);
        var first = new FakeTerminalSession(TerminalSessionId.New());
        var second = new FakeTerminalSession(TerminalSessionId.New());

        _registry.Register(key, first);
        _registry.Register(key, second);

        Assert.That(_registry.Find(key), Is.SameAs(second));
    }

    [Test]
    public void RaisesChangedWithTheIssueAndSession()
    {
        var key = new IssueKey("acme/app", 7);
        var session = new FakeTerminalSession(TerminalSessionId.New());
        IssueSessionChangedEventArgs? seen = null;
        _registry.Changed += (_, e) => seen = e;

        _registry.Register(key, session);

        Assert.That(seen?.Issue, Is.EqualTo(key));
        Assert.That(seen?.Session, Is.SameAs(session));
    }

    [Test]
    public void TreatsRepositoryNamesCaseInsensitively()
    {
        var session = new FakeTerminalSession(TerminalSessionId.New());
        _registry.Register(new IssueKey("Acme/App", 1), session);

        Assert.That(_registry.Find(new IssueKey("acme/app", 1)), Is.SameAs(session));
    }

    [Test]
    public void ForgetsARemovedSession()
    {
        var key = new IssueKey("acme/app", 1);
        var session = new FakeTerminalSession(TerminalSessionId.New());
        _registry.Register(key, session);

        _registry.Remove(key);

        Assert.That(_registry.Find(key), Is.Null);
        Assert.That(_registry.IssueOf(session.Id), Is.Null);
    }
}
