namespace Kontext.GitAutomation.UnitTests;

[TestFixture]
public class WorktreePorcelainParserTests
{
    private static readonly string[] ExpectedBranches = ["main", "kontext/1-x", "(detached)"];

    private const string Output =
        "worktree C:/repos/app\nHEAD 1111111\nbranch refs/heads/main\n\n" +
        "worktree C:/wt/acme/app/1-x\nHEAD 2222222\nbranch refs/heads/kontext/1-x\n\n" +
        "worktree C:/wt/detached\nHEAD 3333333\ndetached\n\n";

    [Test]
    public void ParsesEveryWorktreeAndMarksTheFirstAsMain()
    {
        var worktrees = WorktreePorcelainParser.Parse(Output);

        Assert.That(worktrees, Has.Count.EqualTo(3));
        Assert.That(worktrees[0].IsMain, Is.True);
        Assert.That(worktrees[1].IsMain, Is.False);
        Assert.That(worktrees.Select(worktree => worktree.BranchName), Is.EqualTo(ExpectedBranches));
        Assert.That(worktrees[1].HeadCommit, Is.EqualTo("2222222"));
    }

    [Test]
    public void NormalisesPathsToTheNativeSeparator()
    {
        var worktrees = WorktreePorcelainParser.Parse(Output);

        Assert.That(worktrees[1].Path, Is.EqualTo(Path.GetFullPath("C:/wt/acme/app/1-x")));
    }

    [Test]
    public void HandlesOutputWithoutATrailingBlankLine()
    {
        var worktrees = WorktreePorcelainParser.Parse("worktree C:/repos/app\nHEAD 1111111\nbranch refs/heads/main");

        Assert.That(worktrees, Has.Count.EqualTo(1));
    }

    [Test]
    public void IgnoresAWorktreeWhoseFolderWasDeletedAndSoGitCallsItPrunable()
    {
        const string withStale =
            "worktree C:/repos/app\nHEAD 1111111\nbranch refs/heads/main\n\n" +
            "worktree C:/wt/gone\nHEAD 2222222\nbranch refs/heads/features/1-x\nprunable gitdir file points to non-existent location\n\n" +
            "worktree C:/wt/alive\nHEAD 3333333\nbranch refs/heads/features/2-y\n\n";

        var worktrees = WorktreePorcelainParser.Parse(withStale);

        Assert.That(worktrees.Select(worktree => worktree.BranchName), Is.EqualTo(Expect.Seq("main", "features/2-y")));
    }

    [Test]
    public void ReturnsNothingForEmptyOutput()
    {
        Assert.That(WorktreePorcelainParser.Parse(string.Empty), Is.Empty);
    }
}
