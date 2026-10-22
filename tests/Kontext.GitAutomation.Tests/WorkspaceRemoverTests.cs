using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.GitAutomation.Tests;

[TestFixture]
public class WorkspaceRemoverTests
{
    private const string ClonePath = @"C:\clones\app";
    private const string WorktreesRoot = @"C:\wt";
    private const string Folder = @"C:\wt\acme\app\7-fix";

    private static readonly RepositoryRef Repository = new("acme", "app");

    private FakeWorktreeService _worktrees = null!;
    private FakeGitProcess _git = null!;
    private FakeFolderRemover _folders = null!;
    private WorkspaceRemover _remover = null!;

    [SetUp]
    public void CreateRemover()
    {
        var clones = new FakeCloneStore();
        clones.Save(Repository, ClonePath);
        _worktrees = new FakeWorktreeService().WithListing(ClonePath, new WorktreeInfo(ClonePath, "main", "000", IsMain: true));
        _git = new FakeGitProcess();
        _folders = new FakeFolderRemover();
        _remover = new WorkspaceRemover(clones, _worktrees, _git, _folders, new GitAutomationOptions(@"C:\state", WorktreesRoot, @"C:\repos"));
    }

    private void TheWorktreeIsRegistered()
        => _worktrees.WithListing(ClonePath, new WorktreeInfo(ClonePath, "main", "000", IsMain: true), new WorktreeInfo(Folder, "features/7-fix", "111", IsMain: false));

    [Test]
    public async Task RemovesTheRegisteredWorktreeAndThenDeletesItsBranchFromTheClone()
    {
        TheWorktreeIsRegistered();

        await _remover.RemoveAsync(Repository, Folder, "features/7-fix");

        Assert.That(_worktrees.Removed, Is.EqualTo(Expect.Seq((Folder, true))));
        Assert.That(_worktrees.Pruned, Is.EqualTo(Expect.Seq(ClonePath)));
        Assert.That(_git.Runs.Last().WorkingDirectory, Is.EqualTo(ClonePath));
        Assert.That(_git.Runs.Last().Arguments, Is.EqualTo(Expect.Seq("branch", "-D", "features/7-fix")));
        Assert.That(_folders.Deleted, Is.Empty);
    }

    [Test]
    public async Task FinishesTheDeletingWhenGitLeftFilesBehind()
    {
        TheWorktreeIsRegistered();
        _folders.Existing.Add(Folder);

        await _remover.RemoveAsync(Repository, Folder, "features/7-fix");

        Assert.That(_folders.Deleted, Is.EqualTo(Expect.Seq(Folder)));
    }

    [Test]
    public async Task CarriesOnWhenGitFailsToRemoveAWorktreeWhoseFolderItCanDeleteItself()
    {
        TheWorktreeIsRegistered();
        _worktrees.RemoveFailsWith = new GitAutomationException("failed to delete: Directory not empty");
        _folders.Existing.Add(Folder);

        await _remover.RemoveAsync(Repository, Folder, "features/7-fix");

        Assert.That(_folders.Deleted, Is.EqualTo(Expect.Seq(Folder)));
        Assert.That(_git.Runs.Last().Arguments, Is.EqualTo(Expect.Seq("branch", "-D", "features/7-fix")));
    }

    [Test]
    public async Task CleansUpAFolderGitNoLongerKnowsAboutSoAnEarlierFailedRemovalCanBeRetried()
    {
        _folders.Existing.Add(Folder);

        await _remover.RemoveAsync(Repository, Folder, "features/7-fix");

        Assert.That(_worktrees.Removed, Is.Empty);
        Assert.That(_folders.Deleted, Is.EqualTo(Expect.Seq(Folder)));
        Assert.That(_git.Runs.Last().Arguments, Is.EqualTo(Expect.Seq("branch", "-D", "features/7-fix")));
    }

    [Test]
    public void KeepsTheBranchAndReportsWhyWhenSomethingStillHoldsTheFolder()
    {
        _folders.Existing.Add(Folder);
        _folders.FailWith = new GitAutomationException("in use");

        Assert.That(() => _remover.RemoveAsync(Repository, Folder, "features/7-fix"), Throws.TypeOf<GitAutomationException>().With.Message.EqualTo("in use"));
        Assert.That(_git.Runs, Is.Empty, "the branch is only deleted once the folder is gone");
    }

    [Test]
    public void NeverDeletesAFolderOutsideWhereKontextKeepsItsWorktrees()
    {
        const string outside = @"C:\Users\someone\Documents";
        _folders.Existing.Add(outside);

        Assert.That(() => _remover.RemoveAsync(Repository, outside, "features/7-fix"), Throws.TypeOf<GitAutomationException>());
        Assert.That(_folders.Deleted, Is.Empty);
    }

    [Test]
    public async Task LeavesGitAloneWhenTheBranchIsAlreadyGone()
    {
        _git.Result = new GitResult(1, string.Empty, string.Empty);

        await _remover.RemoveAsync(Repository, Folder, "features/7-fix");

        Assert.That(_git.Runs, Has.Count.EqualTo(1));
        Assert.That(_git.Runs.Single().Arguments[0], Is.EqualTo("show-ref"));
    }

    [Test]
    public void ExplainsWhenTheRepositoryHasNoLocalClone()
    {
        var unknown = new RepositoryRef("acme", "other");

        Assert.That(
            () => _remover.RemoveAsync(unknown, Folder, "features/7-fix"),
            Throws.TypeOf<GitAutomationException>().With.Message.Contains("acme/other"));
    }
}
