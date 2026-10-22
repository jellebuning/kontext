using Kontext.Kernel;

namespace Kontext.GitAutomation.Tests;

[TestFixture]
public class RepositoryCloneResolverTests
{
    private static readonly RepositoryRef Acme = new("acme", "app");

    [Test]
    public async Task UsesTheSavedCloneWithoutCloningWhenItIsStillValid()
    {
        var store = new FakeCloneStore { Saved = { ["acme/app"] = @"C:\clones\app" } };
        var cloner = new FakeCloner(@"C:\unused");
        var inspector = new FakeInspector().WithRepository(@"C:\clones\app", @"C:\clones\app", Acme);

        var resolved = await new RepositoryCloneResolver(store, cloner, inspector).ResolveAsync(Acme);

        Assert.That(resolved.LocalPath, Is.EqualTo(@"C:\clones\app"));
        Assert.That(cloner.Calls, Is.Zero);
    }

    [Test]
    public async Task ClonesAgainWhenTheSavedFolderIsNoLongerARepository()
    {
        var store = new FakeCloneStore { Saved = { ["acme/app"] = @"C:\gone" } };
        var cloner = new FakeCloner(@"C:\repos\acme\app");
        var inspector = new FakeInspector().WithRepository(@"C:\repos\acme\app", @"C:\repos\acme\app", Acme);

        var resolved = await new RepositoryCloneResolver(store, cloner, inspector).ResolveAsync(Acme);

        Assert.That(resolved.LocalPath, Is.EqualTo(@"C:\repos\acme\app"));
        Assert.That(store.Saved["acme/app"], Is.EqualTo(@"C:\repos\acme\app"));
        Assert.That(cloner.Calls, Is.EqualTo(1));
    }

    [Test]
    public async Task ClonesOnFirstUseAndRemembersTheRepositoryRoot()
    {
        var store = new FakeCloneStore();
        var cloner = new FakeCloner(@"C:\repos\acme\app\src");
        var inspector = new FakeInspector().WithRepository(@"C:\repos\acme\app\src", @"C:\repos\acme\app", Acme);

        var resolved = await new RepositoryCloneResolver(store, cloner, inspector).ResolveAsync(Acme);

        Assert.That(resolved.LocalPath, Is.EqualTo(@"C:\repos\acme\app"));
        Assert.That(store.Saved["acme/app"], Is.EqualTo(@"C:\repos\acme\app"));
        Assert.That(cloner.Last, Is.EqualTo(Acme));
    }

    [Test]
    public void RejectsAFolderThatIsNotAGitRepository()
    {
        var resolver = new RepositoryCloneResolver(new FakeCloneStore(), new FakeCloner(@"C:\plain"), new FakeInspector());

        Assert.That(async () => await resolver.ResolveAsync(Acme), Throws.TypeOf<GitAutomationException>().With.Message.Contains("not inside a git repository"));
    }

    [Test]
    public void RejectsAFolderThatIsACloneOfADifferentRepositoryAndSavesNothing()
    {
        var store = new FakeCloneStore();
        var inspector = new FakeInspector().WithRepository(@"C:\other", @"C:\other", new RepositoryRef("acme", "other"));
        var resolver = new RepositoryCloneResolver(store, new FakeCloner(@"C:\other"), inspector);

        Assert.That(async () => await resolver.ResolveAsync(Acme), Throws.TypeOf<GitAutomationException>().With.Message.Contains("acme/other"));
        Assert.That(store.Saved, Is.Empty);
    }

    [Test]
    public void LetsACloneFailureThroughAndSavesNothing()
    {
        var store = new FakeCloneStore();
        var cloner = new FakeCloner(@"C:\x") { FailWith = new GitAutomationException("repository not found") };
        var resolver = new RepositoryCloneResolver(store, cloner, new FakeInspector());

        Assert.That(async () => await resolver.ResolveAsync(Acme), Throws.TypeOf<GitAutomationException>().With.Message.Contains("repository not found"));
        Assert.That(store.Saved, Is.Empty);
    }

    [Test]
    public async Task MatchesRepositoryNamesIgnoringCase()
    {
        var inspector = new FakeInspector().WithRepository(@"C:\repos\acme\app", @"C:\repos\acme\app", new RepositoryRef("Acme", "App"));

        var resolved = await new RepositoryCloneResolver(new FakeCloneStore(), new FakeCloner(@"C:\repos\acme\app"), inspector).ResolveAsync(Acme);

        Assert.That(resolved.LocalPath, Is.EqualTo(@"C:\repos\acme\app"));
    }
}
