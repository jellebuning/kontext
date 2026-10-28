using Kontext.Domain;
using Kontext.Kernel;
using Kontext.TestSupport;

namespace Kontext.GitAutomation.UnitTests;

[TestFixture]
public class ExistingWorkspaceFinderTests
{
    private static WorktreeInfo MainWorktree(string path) => new(path, "main", "000", IsMain: true);

    private static WorktreeInfo Work(string path, string branch) => new(path, branch, "111", IsMain: false);

    [Test]
    public async Task FindsWorktreesNamedForIssuesInEveryRepositoryWithASavedClone()
    {
        var store = new FakeCloneStore { Saved = { ["acme/memento"] = @"C:\clones\memento", ["acme/gallery"] = @"C:\clones\gallery" } };
        var worktrees = new FakeWorktreeService()
            .WithListing(
                @"C:\clones\memento",
                MainWorktree(@"C:\clones\memento"),
                Work(@"C:\wt\memento\4-add", "kontext/4-add-notifications"),
                Work(@"C:\wt\memento\other", "feature/unrelated"))
            .WithListing(
                @"C:\clones\gallery",
                MainWorktree(@"C:\clones\gallery"),
                Work(@"C:\wt\gallery\159-x", "kontext/159-creating-galleries"));

        var found = await new ExistingWorkspaceFinder(store, worktrees, new BranchNamer()).FindAllAsync();

        Assert.That(found.Select(workspace => workspace.Issue), Is.EquivalentTo(Expect.Seq(new IssueKey("acme/memento", 4), new IssueKey("acme/gallery", 159))));
        var memento = found.Single(workspace => workspace.Number == 4);
        Assert.That(memento.Repository, Is.EqualTo(new RepositoryRef("acme", "memento")));
        Assert.That(memento.Worktree.Path, Is.EqualTo(@"C:\wt\memento\4-add"));
        Assert.That(memento.Worktree.BranchName, Is.EqualTo("kontext/4-add-notifications"));
    }

    [Test]
    public async Task KeepsTheRepositoryNameAsGitHubSpellsItNotUpperCased()
    {
        var store = new FakeCloneStore { Saved = { ["GalleryPixels/GalleryPixels"] = @"C:\clones\gp" } };
        var worktrees = new FakeWorktreeService().WithListing(@"C:\clones\gp", MainWorktree(@"C:\clones\gp"), Work(@"C:\wt\gp\159-x", "kontext/159-creating-galleries"));

        var found = await new ExistingWorkspaceFinder(store, worktrees, new BranchNamer()).FindAllAsync();

        Assert.That(found.Single().Repository.FullName, Is.EqualTo("GalleryPixels/GalleryPixels"));
    }

    [Test]
    public async Task SkipsACloneThatCannotBeReadAndKeepsTheRest()
    {
        var store = new FakeCloneStore { Saved = { ["acme/memento"] = @"C:\gone", ["acme/gallery"] = @"C:\clones\gallery" } };
        var worktrees = new FakeWorktreeService().WithListing(
            @"C:\clones\gallery",
            MainWorktree(@"C:\clones\gallery"),
            Work(@"C:\wt\gallery\159-x", "kontext/159-creating-galleries"));

        var found = await new ExistingWorkspaceFinder(store, worktrees, new BranchNamer()).FindAllAsync();

        Assert.That(found.Select(workspace => workspace.Issue), Is.EqualTo(Expect.Seq(new IssueKey("acme/gallery", 159))));
    }

    [Test]
    public async Task IgnoresTheMainWorktreeEvenIfItsBranchLooksLikeOurs()
    {
        var store = new FakeCloneStore { Saved = { ["acme/memento"] = @"C:\clones\memento" } };
        var worktrees = new FakeWorktreeService().WithListing(
            @"C:\clones\memento",
            new WorktreeInfo(@"C:\clones\memento", "kontext/4-x", "000", IsMain: true));

        var found = await new ExistingWorkspaceFinder(store, worktrees, new BranchNamer()).FindAllAsync();

        Assert.That(found, Is.Empty);
    }

    [Test]
    public async Task ReturnsNothingWithoutTouchingGitWhenNoCloneIsKnown()
    {
        var worktrees = new FakeWorktreeService();

        var found = await new ExistingWorkspaceFinder(new FakeCloneStore(), worktrees, new BranchNamer()).FindAllAsync();

        Assert.That(found, Is.Empty);
        Assert.That(worktrees.ListedPaths, Is.Empty);
    }
}
