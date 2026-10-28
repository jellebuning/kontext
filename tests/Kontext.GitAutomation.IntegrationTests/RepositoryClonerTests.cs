using Kontext.Kernel;
using Kontext.TestSupport;

namespace Kontext.GitAutomation.IntegrationTests;

[TestFixture]
public class RepositoryClonerTests
{
    private static readonly RepositoryRef Acme = new("acme", "app");

    private TempDirectory _temp = null!;
    private FakeGitProcess _git = null!;
    private RepositoryCloner _cloner = null!;

    [SetUp]
    public void CreateCloner()
    {
        _temp = new TempDirectory();
        _git = new FakeGitProcess();
        _cloner = new RepositoryCloner(new GitAutomationOptions(_temp.Combine("state"), _temp.Combine("worktrees"), _temp.Combine("repos")), _git, new FolderRemover());
    }

    [TearDown]
    public void DeleteFolders() => _temp.Dispose();

    [Test]
    public async Task ClonesFromGitHubIntoOwnerAndNameUnderTheClonesRoot()
    {
        var target = await _cloner.CloneAsync(Acme);

        var expected = _temp.Combine("repos", "acme", "app");
        Assert.That(target, Is.EqualTo(expected));

        var run = _git.Runs.Single();
        Assert.That(run.Arguments, Is.EqualTo(Expect.Seq("clone", "https://github.com/acme/app.git", expected)));
        Assert.That(run.WorkingDirectory, Is.EqualTo(_temp.Combine("repos", "acme")), "git runs in the (created) parent folder");
        Assert.That(Directory.Exists(run.WorkingDirectory), Is.True);
    }

    [Test]
    public async Task LeavesAnExistingCloneAlone()
    {
        var existing = _temp.Combine("repos", "acme", "app");
        Directory.CreateDirectory(Path.Combine(existing, ".git"));
        await File.WriteAllTextAsync(Path.Combine(existing, "README.md"), "already here");

        var target = await _cloner.CloneAsync(Acme);

        Assert.That(target, Is.EqualTo(existing));
        Assert.That(_git.Runs, Is.Empty);
    }

    [Test]
    public async Task ReplacesAnUnfinishedCloneInsteadOfTreatingItAsDone()
    {
        var leftover = _temp.Combine("repos", "acme", "app");
        Directory.CreateDirectory(Path.Combine(leftover, "src"));
        await File.WriteAllTextAsync(Path.Combine(leftover, "src", "half.cs"), "interrupted");

        await _cloner.CloneAsync(Acme);

        Assert.That(_git.Runs, Has.Count.EqualTo(1));
        Assert.That(File.Exists(Path.Combine(leftover, "src", "half.cs")), Is.False);
    }

    [Test]
    public async Task ClonesOnceWhenTwoIssuesOfTheSameRepositoryAskAtTheSameTime()
    {
        var git = new SlowCloningGit();
        var cloner = new RepositoryCloner(new GitAutomationOptions(_temp.Combine("state"), _temp.Combine("worktrees"), _temp.Combine("repos")), git, new FolderRemover());

        var first = cloner.CloneAsync(Acme);
        var second = cloner.CloneAsync(Acme);
        await Task.Delay(100);
        git.Finish();
        await Task.WhenAll(first, second);

        Assert.That(git.Clones, Is.EqualTo(1));
    }

    [Test]
    public async Task ClonesIntoAnEmptyLeftoverFolder()
    {
        Directory.CreateDirectory(_temp.Combine("repos", "acme", "app"));

        await _cloner.CloneAsync(Acme);

        Assert.That(_git.Runs, Has.Count.EqualTo(1));
    }

    [Test]
    public void ReportsAFailedCloneWithGitsMessage()
    {
        _git.Result = new GitResult(128, string.Empty, "remote: Repository not found.");

        Assert.That(async () => await _cloner.CloneAsync(Acme), Throws.TypeOf<GitAutomationException>().With.Message.Contains("Repository not found"));
    }

    private sealed class SlowCloningGit : IGitProcess
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Clones { get; private set; }

        public void Finish() => _release.SetResult();

        public async Task<GitResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            Clones++;
            await _release.Task;
            Directory.CreateDirectory(Path.Combine(arguments[2], ".git"));
            return new GitResult(0, string.Empty, string.Empty);
        }
    }
}