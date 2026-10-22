using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Sessions.Tests;

[TestFixture]
public class JsonSessionStoreTests
{
    private string _root = null!;

    [SetUp]
    public void CreateScratchDirectory()
    {
        _root = Path.Combine(Path.GetTempPath(), "kontext-tests", Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void DeleteScratchDirectory()
    {
        foreach (var file in Directory.EnumerateFiles(_root))
        {
            File.Delete(file);
        }

        Directory.Delete(_root);
    }

    private JsonSessionStore CreateStore() => new(new SessionsOptions(_root));

    private static StoredSession Session(string repository = "acme/app", int number = 4, string path = @"C:\wt\4-x")
        => new(new IssueKey(repository, number), TerminalSessionId.New(), path, "kontext/4-x");

    [Test]
    public void FindsNothingBeforeAnythingIsSaved()
    {
        Assert.That(CreateStore().Find(new IssueKey("acme/app", 4)), Is.Null);
    }

    [Test]
    public void RoundTripsASession()
    {
        var saved = Session();
        CreateStore().Save(saved);

        var found = CreateStore().Find(saved.Issue);

        Assert.That(found, Is.EqualTo(saved));
    }

    [Test]
    public void KeepsEachIssuesSessionSeparate()
    {
        var store = CreateStore();
        var first = Session(number: 4);
        var second = Session(number: 5, path: @"C:\wt\5-y");
        store.Save(first);
        store.Save(second);

        Assert.That(store.Find(first.Issue), Is.EqualTo(first));
        Assert.That(store.Find(second.Issue), Is.EqualTo(second));
    }

    [Test]
    public void ReplacesAnEarlierSessionForTheSameIssue()
    {
        var store = CreateStore();
        var issue = new IssueKey("acme/app", 4);
        store.Save(new StoredSession(issue, TerminalSessionId.New(), @"C:\old", "kontext/4-old"));
        var newer = new StoredSession(issue, TerminalSessionId.New(), @"C:\new", "kontext/4-new");

        store.Save(newer);

        Assert.That(store.Find(issue), Is.EqualTo(newer));
    }

    [Test]
    public void IgnoresRepositoryCaseWhenLookingUp()
    {
        CreateStore().Save(Session(repository: "Acme/App"));

        Assert.That(CreateStore().Find(new IssueKey("acme/app", 4)), Is.Not.Null);
    }

    [Test]
    public void TreatsACorruptFileAsEmptyAndRecoversOnNextSave()
    {
        File.WriteAllText(Path.Combine(_root, "sessions.json"), "{ not json");
        var store = CreateStore();

        Assert.That(store.Find(new IssueKey("acme/app", 4)), Is.Null);

        var saved = Session();
        store.Save(saved);
        Assert.That(store.Find(saved.Issue), Is.EqualTo(saved));
    }

    [Test]
    public void CreatesTheDataDirectoryWhenItDoesNotExistYet()
    {
        var nested = new JsonSessionStore(new SessionsOptions(Path.Combine(_root, "not", "yet")));
        var saved = Session();

        nested.Save(saved);

        Assert.That(nested.Find(saved.Issue), Is.EqualTo(saved));

        File.Delete(Path.Combine(_root, "not", "yet", "sessions.json"));
        Directory.Delete(Path.Combine(_root, "not", "yet"));
        Directory.Delete(Path.Combine(_root, "not"));
    }

    [Test]
    public void ForgetsARemovedSessionAndKeepsTheOthers()
    {
        var store = CreateStore();
        var removed = Session(number: 4);
        var kept = Session(number: 5, path: @"C:\wt\5-y");
        store.Save(removed);
        store.Save(kept);

        store.Remove(removed.Issue);

        Assert.That(CreateStore().Find(removed.Issue), Is.Null);
        Assert.That(CreateStore().Find(kept.Issue), Is.EqualTo(kept));
    }

    [Test]
    public void RemovingASessionThatWasNeverSavedChangesNothing()
    {
        Assert.That(() => CreateStore().Remove(new IssueKey("acme/app", 4)), Throws.Nothing);
    }
}
