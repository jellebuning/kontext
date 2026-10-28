using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.GitAutomation.UnitTests;

[TestFixture]
public class WorkspaceProvisionerTests
{
    private const string ClonePath = @"C:\clones\app";

    private static readonly RepositoryRef Clone = new("acme", "app", ClonePath);

    private static WorktreeInfo Main => new(ClonePath, "main", "000", IsMain: true);

    private static WorktreeInfo Work(string folder, string branch) => new($@"C:\wt\{folder}", branch, "111", IsMain: false);

    private static WorkspaceProvisioner Provisioner(FakeWorktreeService worktrees) => new(new FakeCloneResolver(Clone), new BranchNamer(), worktrees);

    [Test]
    public async Task CreatesAWorktreeOnTheNamedBranchInTheResolvedClone()
    {
        var worktrees = new FakeWorktreeService().WithListing(ClonePath, Main);

        var result = await Provisioner(worktrees).ProvisionAsync(TestIssues.Create(7, "Fix login crash"));

        Assert.That(result.BranchName, Is.EqualTo("features/7-fix-login-crash"));
        Assert.That(worktrees.LastRequest?.Repository, Is.EqualTo(Clone));
        Assert.That(worktrees.LastRequest?.BranchName, Is.EqualTo("features/7-fix-login-crash"));
    }

    [Test]
    public async Task ReusesTheWorktreeAnIssueAlreadyHasWithoutCreatingAnother()
    {
        var existing = Work("7-fix-login-crash", "features/7-fix-login-crash");
        var worktrees = new FakeWorktreeService().WithListing(ClonePath, Main, existing);

        var result = await Provisioner(worktrees).ProvisionAsync(TestIssues.Create(7, "Fix login crash"));

        Assert.That(result, Is.EqualTo(existing));
        Assert.That(worktrees.LastRequest, Is.Null, "nothing was created");
    }

    [Test]
    public async Task ReusesAWorktreeMadeUnderTheEarlierBranchPrefix()
    {
        var existing = Work("7-fix-login-crash", "kontext/7-fix-login-crash");
        var worktrees = new FakeWorktreeService().WithListing(ClonePath, Main, existing);

        var result = await Provisioner(worktrees).ProvisionAsync(TestIssues.Create(7, "Fix login crash"));

        Assert.That(result, Is.EqualTo(existing));
        Assert.That(worktrees.LastRequest, Is.Null);
    }

    [Test]
    public async Task ReusesTheExistingWorktreeEvenIfTheIssueWasRetitled()
    {
        var existing = Work("7-fix-login-crash", "features/7-fix-login-crash");
        var worktrees = new FakeWorktreeService().WithListing(ClonePath, Main, existing);

        var result = await Provisioner(worktrees).ProvisionAsync(TestIssues.Create(7, "Fix the login crash properly"));

        Assert.That(result, Is.EqualTo(existing), "the title is not part of what identifies the issue");
    }

    [Test]
    public async Task IgnoresWorktreesOfOtherIssuesAndTheMainWorktree()
    {
        var worktrees = new FakeWorktreeService().WithListing(
            ClonePath,
            new WorktreeInfo(ClonePath, "features/7-fix", "000", IsMain: true),
            Work("8-other", "features/8-other"),
            Work("unrelated", "feature/7-not-ours"));

        await Provisioner(worktrees).ProvisionAsync(TestIssues.Create(7, "Fix login crash"));

        Assert.That(worktrees.LastRequest?.BranchName, Is.EqualTo("features/7-fix-login-crash"));
    }
}
