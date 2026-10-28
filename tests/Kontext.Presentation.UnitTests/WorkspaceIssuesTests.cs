using Kontext.Domain;
using Kontext.Kernel;
using Kontext.TestSupport;

namespace Kontext.Presentation.UnitTests;

[TestFixture]
public class WorkspaceIssuesTests
{
    private FakeGitHubService _gitHub = null!;
    private WorkspaceIssues _finder = null!;

    [SetUp]
    public void CreateFinder()
    {
        _gitHub = new FakeGitHubService();
        _finder = new WorkspaceIssues(_gitHub);
    }

    private static ExistingWorkspace Workspace(int number, string owner = "acme", string name = "memento")
        => new(new RepositoryRef(owner, name), number, new WorktreeInfo($@"C:\wt\{number}", $"kontext/{number}-x", "abc", IsMain: false));

    [Test]
    public async Task FetchesTheIssuesOfWorktreesThatAreNotInTheShownList()
    {
        var shown = new[] { Issues.Create(4, "Open one", "acme/memento") };
        _gitHub.ClosedIssues.Add(Issues.Create(7, "Closed one", "acme/memento") with { State = "closed" });

        var missing = await _finder.FindMissingAsync(shown, [Workspace(4), Workspace(7)]);

        Assert.That(missing.Select(issue => issue.Number), Is.EqualTo(Expect.Seq(7)));
        Assert.That(missing.Single().State, Is.EqualTo("closed"));
    }

    [Test]
    public async Task AsksForNothingWhenEveryWorktreeIsAlreadyShown()
    {
        var shown = new[] { Issues.Create(4, "Open one", "acme/memento") };

        var missing = await _finder.FindMissingAsync(shown, [Workspace(4)]);

        Assert.That(missing, Is.Empty);
    }

    [Test]
    public async Task SkipsAnIssueGitHubCannotProvideAndKeepsTheOthers()
    {
        _gitHub.ClosedIssues.Add(Issues.Create(8, "Still there", "acme/memento") with { State = "closed" });

        var missing = await _finder.FindMissingAsync([], [Workspace(7), Workspace(8)]);

        Assert.That(missing.Select(issue => issue.Number), Is.EqualTo(Expect.Seq(8)));
    }

    [Test]
    public async Task MatchesTheShownListIgnoringRepositoryCase()
    {
        var shown = new[] { Issues.Create(4, "Open one", "Acme/Memento") };

        var missing = await _finder.FindMissingAsync(shown, [Workspace(4, "ACME", "memento")]);

        Assert.That(missing, Is.Empty);
    }

    [Test]
    public async Task FetchesAnIssueOnlyOnceEvenIfItHasTwoWorktrees()
    {
        _gitHub.ClosedIssues.Add(Issues.Create(7, "Closed one", "acme/memento") with { State = "closed" });

        var missing = await _finder.FindMissingAsync([], [Workspace(7), Workspace(7)]);

        Assert.That(missing, Has.Count.EqualTo(1));
    }
}
