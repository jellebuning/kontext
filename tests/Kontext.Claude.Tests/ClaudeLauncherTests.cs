using Kontext.Domain;
using Kontext.Kernel;
using Kontext.TestSupport;

namespace Kontext.Claude.Tests;

[TestFixture]
public class ClaudeLauncherTests
{
    private static readonly WorktreeInfo Worktree = new(@"C:\wt\4-add", "features/4-add", "abc", IsMain: false);
    private static readonly IssueKey Key = new("acme/memento", 4);

    private FakeClaudeTranscripts _transcripts = null!;
    private FakeClaudeHooks _hooks = null!;
    private ClaudeLauncher _launcher = null!;

    [SetUp]
    public void CreateLauncher()
    {
        _transcripts = new FakeClaudeTranscripts();
        _hooks = new FakeClaudeHooks();
        _launcher = new ClaudeLauncher(new ClaudeLaunchBuilder(new ClaudeOptions(@"C:\claude")), _transcripts, _hooks);
    }

    private static StoredSession Stored(TerminalSessionId id, string worktreePath = @"C:\wt\4-add")
        => new(Key, id, worktreePath, "features/4-add");

    [Test]
    public void StartsAnEmptyClaudeSessionInTheWorktreeLeavingTheFirstMessageToTheUser()
    {
        var launch = _launcher.Prepare(null, Worktree);

        Assert.That(launch.Options.WorkingDirectory, Is.EqualTo(@"C:\wt\4-add"));
        Assert.That(launch.Options.Arguments, Is.EqualTo(Expect.Seq("--session-id", launch.SessionId.Value.ToString("D"), "--permission-mode", "acceptEdits", "--settings", FakeClaudeHooks.DefaultSettings)));
    }

    [Test]
    public void ReportsThatTheSessionReportsItselfWhenHooksAreAvailable()
    {
        Assert.That(_launcher.Prepare(null, Worktree).ReportsItself, Is.True);
    }

    [Test]
    public void LaunchesWithoutHooksAndSaysSoWhenKontextCannotReceiveReports()
    {
        _hooks.Settings = null;

        var launch = _launcher.Prepare(null, Worktree);

        Assert.That(launch.ReportsItself, Is.False);
        Assert.That(launch.Options.Arguments, Does.Not.Contain("--settings"));
    }

    [Test]
    public void AsksForTheHooksOfTheSessionItIsLaunching()
    {
        var launch = _launcher.Prepare(null, Worktree);

        Assert.That(_hooks.Asked, Is.EqualTo(Expect.Seq(launch.SessionId)));
    }

    [Test]
    public void ResumesTheStoredSessionWhenItRanInTheSameWorktreeAndLeftAConversation()
    {
        var earlier = TerminalSessionId.New();
        _transcripts.Saved.Add(earlier);

        var launch = _launcher.Prepare(Stored(earlier), Worktree);

        Assert.That(launch.SessionId, Is.EqualTo(earlier));
        Assert.That(launch.Resumed, Is.True);
        Assert.That(launch.Options.Arguments, Is.EqualTo(Expect.Seq("--resume", earlier.Value.ToString("D"), "--permission-mode", "acceptEdits", "--settings", FakeClaudeHooks.DefaultSettings)));
    }

    [Test]
    public void StartsAFreshEmptySessionWhenTheStoredOneNeverSavedAConversation()
    {
        var earlier = TerminalSessionId.New();

        var launch = _launcher.Prepare(Stored(earlier), Worktree);

        Assert.That(launch.SessionId, Is.Not.EqualTo(earlier));
        Assert.That(launch.Resumed, Is.False, "an empty session has no conversation to continue");
        Assert.That(launch.Options.Arguments[0], Is.EqualTo("--session-id"));
    }

    [Test]
    public void ComparesWorktreePathsIgnoringCaseAndSeparators()
    {
        var earlier = TerminalSessionId.New();
        _transcripts.Saved.Add(earlier);

        var launch = _launcher.Prepare(Stored(earlier, @"c:\WT\4-add\"), Worktree);

        Assert.That(launch.Options.Arguments[0], Is.EqualTo("--resume"));
    }

    [Test]
    public void StartsAFreshSessionWhenTheStoredOneRanSomewhereElse()
    {
        var earlier = TerminalSessionId.New();
        _transcripts.Saved.Add(earlier);

        var launch = _launcher.Prepare(Stored(earlier, @"C:\somewhere\else"), Worktree);

        Assert.That(launch.SessionId, Is.Not.EqualTo(earlier));
        Assert.That(launch.Options.Arguments[0], Is.EqualTo("--session-id"));
    }

    [Test]
    public void RefusesANullWorktree()
    {
        Assert.That(() => _launcher.Prepare(null, null!), Throws.ArgumentNullException);
    }
}
