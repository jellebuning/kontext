using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.GitAutomation.Tests;

[TestFixture]
public class GitWorktreeServiceIntegrationTests
{
    private static readonly string[] ExpectedRemotes = ["acme/app"];

    private readonly GitProcess _git = new(new NoGitAuthentication());
    private TempDirectory _temp = null!;
    private GitAutomationOptions _options = null!;
    private GitWorktreeService _service = null!;
    private string _clone = null!;
    private RepositoryRef _repository = null!;

    [SetUp]
    public async Task CreateRepository()
    {
        _temp = new TempDirectory();
        _options = new GitAutomationOptions(_temp.Combine("state"), _temp.Combine("worktrees"), _temp.Combine("repos"));
        _service = new GitWorktreeService(_git, new WorktreePathProvider(_options));

        _clone = _temp.Combine("clone");
        Directory.CreateDirectory(_clone);
        await _git.RunCheckedAsync(_clone, ["init", "--initial-branch=main"]);
        await _git.RunCheckedAsync(_clone, ["config", "user.email", "test@example.com"]);
        await _git.RunCheckedAsync(_clone, ["config", "user.name", "Test"]);
        await File.WriteAllTextAsync(Path.Combine(_clone, "README.md"), "hello");
        await _git.RunCheckedAsync(_clone, ["add", "."]);
        await _git.RunCheckedAsync(_clone, ["commit", "-m", "initial"]);

        _repository = new RepositoryRef("acme", "app", _clone);
    }

    [TearDown]
    public void DeleteRepository() => _temp.Dispose();

    [Test]
    public async Task CreatesAWorktreeOnANewBranchInTheConfiguredLocation()
    {
        var worktree = await _service.CreateAsync(new WorktreeRequest(_repository, "kontext/1-fix"));

        Assert.That(worktree.BranchName, Is.EqualTo("kontext/1-fix"));
        Assert.That(worktree.IsMain, Is.False);
        Assert.That(worktree.Path, Is.EqualTo(Path.GetFullPath(_temp.Combine("worktrees", "acme", "app", "1-fix"))));
        Assert.That(File.Exists(Path.Combine(worktree.Path, "README.md")), Is.True);

        var branch = await _git.RunCheckedAsync(worktree.Path, ["branch", "--show-current"]);
        Assert.That(branch.Output, Is.EqualTo("kontext/1-fix"));
    }

    [Test]
    public async Task ReturnsTheExistingWorktreeWhenTheBranchAlreadyHasOne()
    {
        var first = await _service.CreateAsync(new WorktreeRequest(_repository, "kontext/1-fix"));

        var second = await _service.CreateAsync(new WorktreeRequest(_repository, "kontext/1-fix"));

        Assert.That(second.Path, Is.EqualTo(first.Path));
        Assert.That(await _service.ListAsync(_clone), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ListsTheMainWorktreeFirstThenTheNewOne()
    {
        await _service.CreateAsync(new WorktreeRequest(_repository, "kontext/1-fix"));

        var worktrees = await _service.ListAsync(_clone);

        Assert.That(worktrees, Has.Count.EqualTo(2));
        Assert.That(worktrees[0].IsMain, Is.True);
        Assert.That(worktrees[0].BranchName, Is.EqualTo("main"));
        Assert.That(worktrees[1].BranchName, Is.EqualTo("kontext/1-fix"));
    }

    [Test]
    public async Task ReusesAnExistingBranchWhenItsWorktreeWasRemoved()
    {
        var first = await _service.CreateAsync(new WorktreeRequest(_repository, "kontext/1-fix"));
        await _service.RemoveAsync(first.Path);

        var second = await _service.CreateAsync(new WorktreeRequest(_repository, "kontext/1-fix"));

        Assert.That(Directory.Exists(second.Path), Is.True);
        Assert.That(second.BranchName, Is.EqualTo("kontext/1-fix"));
    }

    [Test]
    public async Task ARealWorktreeIsRediscoveredByTheFinderInALaterRun()
    {
        var issue = TestIssues.Create(4, "Add notifications", "acme", "app");
        var branch = new BranchNamer().Name(issue);
        var created = await _service.CreateAsync(new WorktreeRequest(_repository, branch));
        var store = new FakeCloneStore { Saved = { ["acme/app"] = _clone } };

        var found = await new ExistingWorkspaceFinder(store, _service, new BranchNamer()).FindAllAsync();

        var workspace = found.Single();
        Assert.That(workspace.Issue, Is.EqualTo(new IssueKey("acme/app", 4)));
        Assert.That(workspace.Worktree.Path, Is.EqualTo(created.Path));
    }

    [Test]
    public async Task ProvisioningAnIssueWhoseWorktreeWasMadeUnderTheEarlierPrefixReusesItInsteadOfFailing()
    {
        var issue = TestIssues.Create(4, "Add notifications", "acme", "app");
        var earlier = await _service.CreateAsync(new WorktreeRequest(_repository, "kontext/4-add-notifications"));
        var provisioner = new WorkspaceProvisioner(new FakeCloneResolver(_repository), new BranchNamer(), _service);

        var result = await provisioner.ProvisionAsync(issue);

        Assert.That(result.Path, Is.EqualTo(earlier.Path));
        Assert.That(result.BranchName, Is.EqualTo("kontext/4-add-notifications"));
        Assert.That(await _service.ListAsync(_clone), Has.Count.EqualTo(2), "no second worktree was added");
    }

    [Test]
    public async Task RemovesTheWorktreeFolderAndRegistration()
    {
        var worktree = await _service.CreateAsync(new WorktreeRequest(_repository, "kontext/1-fix"));

        await _service.RemoveAsync(worktree.Path);

        Assert.That(Directory.Exists(worktree.Path), Is.False);
        Assert.That(await _service.ListAsync(_clone), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task RefusesToRemoveAWorktreeWithUncommittedChangesUnlessForced()
    {
        var worktree = await _service.CreateAsync(new WorktreeRequest(_repository, "kontext/1-fix"));
        await File.WriteAllTextAsync(Path.Combine(worktree.Path, "README.md"), "edited");

        Assert.That(async () => await _service.RemoveAsync(worktree.Path), Throws.TypeOf<GitAutomationException>());
        Assert.That(Directory.Exists(worktree.Path), Is.True);

        await _service.RemoveAsync(worktree.Path, force: true);
        Assert.That(Directory.Exists(worktree.Path), Is.False);
    }

    [Test]
    public async Task StartsFromTheRequestedBaseBranch()
    {
        await _git.RunCheckedAsync(_clone, ["checkout", "-b", "develop"]);
        await File.WriteAllTextAsync(Path.Combine(_clone, "develop-only.txt"), "x");
        await _git.RunCheckedAsync(_clone, ["add", "."]);
        await _git.RunCheckedAsync(_clone, ["commit", "-m", "develop work"]);
        await _git.RunCheckedAsync(_clone, ["checkout", "main"]);

        var worktree = await _service.CreateAsync(new WorktreeRequest(_repository, "kontext/2-x", BaseBranch: "develop"));

        Assert.That(File.Exists(Path.Combine(worktree.Path, "develop-only.txt")), Is.True);
    }

    [Test]
    public void RequiresALocalClonePath()
    {
        var request = new WorktreeRequest(new RepositoryRef("acme", "app"), "kontext/1-fix");

        Assert.That(async () => await _service.CreateAsync(request), Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public async Task InspectorFindsTheRepositoryRootFromASubfolder()
    {
        var subfolder = Path.Combine(_clone, "src", "deep");
        Directory.CreateDirectory(subfolder);

        var root = await new GitRepositoryInspector(_git).FindRepositoryRootAsync(subfolder);

        Assert.That(root, Is.Not.Null);
        Assert.That(Path.GetFileName(root), Is.EqualTo("clone"));
    }

    [Test]
    public async Task InspectorReturnsNullOutsideARepository()
    {
        var outside = _temp.Combine("not-a-repo");
        Directory.CreateDirectory(outside);

        Assert.That(await new GitRepositoryInspector(_git).FindRepositoryRootAsync(outside), Is.Null);
    }

    [Test]
    public async Task InspectorReadsGitHubRemotesAndIgnoresOthers()
    {
        await _git.RunCheckedAsync(_clone, ["remote", "add", "origin", "git@github.com:acme/app.git"]);
        await _git.RunCheckedAsync(_clone, ["remote", "add", "mirror", "https://gitlab.com/acme/app.git"]);

        var remotes = await new GitRepositoryInspector(_git).GetGitHubRemotesAsync(_clone);

        Assert.That(remotes.Select(remote => remote.FullName), Is.EqualTo(ExpectedRemotes));
    }

    [Test]
    public async Task InspectorReturnsNoRemotesForARepositoryWithoutAny()
    {
        Assert.That(await new GitRepositoryInspector(_git).GetGitHubRemotesAsync(_clone), Is.Empty);
    }

    [Test]
    public async Task GitProcessReportsAMissingDirectoryAsAFailedResultNotAnException()
    {
        var result = await _git.RunAsync(_temp.Combine("does-not-exist"), ["status"]);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.StandardError, Does.Contain("not found"));
    }

    private WorkspaceRemover RemoverFor(FakeCloneStore cloneStore) => new(cloneStore, _service, _git, new FolderRemover(), _options);

    [Test]
    public async Task ARemovedWorkspaceTakesItsDirtyWorktreeAndUnmergedBranchWithIt()
    {
        var cloneStore = new FakeCloneStore();
        cloneStore.Save(_repository, _clone);
        var worktree = await _service.CreateAsync(new WorktreeRequest(_repository, "features/1-fix"));
        await File.WriteAllTextAsync(Path.Combine(worktree.Path, "scratch.txt"), "uncommitted");
        await File.WriteAllTextAsync(Path.Combine(worktree.Path, "work.txt"), "committed");
        await _git.RunCheckedAsync(worktree.Path, ["add", "work.txt"]);
        await _git.RunCheckedAsync(worktree.Path, ["commit", "-m", "work"]);

        await RemoverFor(cloneStore).RemoveAsync(_repository, worktree.Path, worktree.BranchName);

        Assert.That(Directory.Exists(worktree.Path), Is.False);
        Assert.That(await _service.ListAsync(_clone), Has.Count.EqualTo(1));
        var branches = await _git.RunCheckedAsync(_clone, ["branch", "--list", "features/1-fix"]);
        Assert.That(branches.Output, Is.Empty);
    }

    [Test]
    public async Task ClearingAgainFinishesTheJobWhenAnEarlierRemovalLeftFilesBehind()
    {
        var cloneStore = new FakeCloneStore();
        cloneStore.Save(_repository, _clone);
        var worktree = await _service.CreateAsync(new WorktreeRequest(_repository, "features/1-fix"));
        await _service.RemoveAsync(worktree.Path, force: true);
        Directory.CreateDirectory(Path.Combine(worktree.Path, "src", "bin"));
        await File.WriteAllTextAsync(Path.Combine(worktree.Path, "src", "bin", "left.dll"), "locked earlier");

        await RemoverFor(cloneStore).RemoveAsync(_repository, worktree.Path, worktree.BranchName);

        Assert.That(Directory.Exists(worktree.Path), Is.False);
        var branches = await _git.RunCheckedAsync(_clone, ["branch", "--list", "features/1-fix"]);
        Assert.That(branches.Output, Is.Empty);
    }

    [Test]
    public async Task NewBranchesDoNotTrackTheBranchTheyWereCutFromSoAPushCannotLandOnMain()
    {
        await _git.RunCheckedAsync(_clone, ["remote", "add", "origin", _clone]);
        await _git.RunCheckedAsync(_clone, ["fetch", "origin"]);
        await _git.RunCheckedAsync(_clone, ["update-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/main"]);
        await _git.RunCheckedAsync(_clone, ["symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/main"]);

        var worktree = await _service.CreateAsync(new WorktreeRequest(_repository, "features/1-fix"));

        var upstream = await _git.RunAsync(worktree.Path, ["rev-parse", "--abbrev-ref", "features/1-fix@{upstream}"]);
        Assert.That(upstream.Succeeded, Is.False, "a branch with an upstream is pushed to it, which would be main");
        var remote = await _git.RunAsync(_clone, ["config", "--get", "branch.features/1-fix.remote"]);
        Assert.That(remote.Output, Is.Empty);
    }

    [Test]
    public async Task ForgetsAWorktreeWhoseFolderWasDeletedByHandAndCreatesItAgain()
    {
        var first = await _service.CreateAsync(new WorktreeRequest(_repository, "features/1-fix"));
        Directory.Delete(first.Path, recursive: true);

        var again = await _service.CreateAsync(new WorktreeRequest(_repository, "features/1-fix"));

        Assert.That(Directory.Exists(again.Path), Is.True);
        Assert.That(File.Exists(Path.Combine(again.Path, "README.md")), Is.True);
    }
}