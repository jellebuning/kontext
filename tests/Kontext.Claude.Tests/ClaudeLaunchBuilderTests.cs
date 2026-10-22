using Kontext.Domain;
using Kontext.TestSupport;

namespace Kontext.Claude.Tests;

[TestFixture]
public class ClaudeLaunchBuilderTests
{
    private static readonly TerminalSessionId Id = new(Guid.Parse("6d8f503c-c671-e387-7413-d0d074951f3a"));

    private readonly ClaudeLaunchBuilder _builder = new(new ClaudeOptions(@"C:\claude"));

    [Test]
    public void StartsAnEmptySessionWithOurIdAndThePermissionModeAndNoPrompt()
    {
        var options = _builder.BuildNew(Id, @"C:\wt\4-add");

        Assert.That(options.Command, Is.EqualTo("claude"));
        Assert.That(options.WorkingDirectory, Is.EqualTo(@"C:\wt\4-add"));
        Assert.That(options.Arguments, Is.EqualTo(Expect.Seq("--session-id", "6d8f503c-c671-e387-7413-d0d074951f3a", "--permission-mode", "acceptEdits")));
    }

    [Test]
    public void NeverPassesAFirstMessageSoTheUserWritesIt()
    {
        var options = _builder.BuildNew(Id, @"C:\wt\4-add");

        Assert.That(options.Arguments, Has.Count.EqualTo(4));
        Assert.That(options.Arguments[0], Does.StartWith("--"));
        Assert.That(options.Arguments[2], Does.StartWith("--"));
    }

    [Test]
    public void ResumesAnEarlierSessionInTheSameFolder()
    {
        var options = _builder.BuildResume(Id, @"C:\wt\4-add");

        Assert.That(options.WorkingDirectory, Is.EqualTo(@"C:\wt\4-add"));
        Assert.That(options.Arguments, Is.EqualTo(Expect.Seq("--resume", "6d8f503c-c671-e387-7413-d0d074951f3a", "--permission-mode", "acceptEdits")));
    }

    [Test]
    public void UsesTheDashedFormOfTheIdBecauseClaudeRequiresAUuid()
    {
        Assert.That(Guid.TryParseExact(_builder.BuildNew(Id, @"C:\wt").Arguments[1], "D", out _), Is.True);
        Assert.That(Guid.TryParseExact(_builder.BuildResume(Id, @"C:\wt").Arguments[1], "D", out _), Is.True);
    }

    [Test]
    public void HonoursConfiguredCommandAndPermissionMode()
    {
        var builder = new ClaudeLaunchBuilder(new ClaudeOptions(@"C:\claude", Command: @"C:\tools\claude.exe", PermissionMode: "plan"));

        var fresh = builder.BuildNew(Id, @"C:\wt");
        var resumed = builder.BuildResume(Id, @"C:\wt");

        Assert.That(fresh.Command, Is.EqualTo(@"C:\tools\claude.exe"));
        Assert.That(fresh.Arguments, Does.Contain("plan").And.Not.Contain("acceptEdits"));
        Assert.That(resumed.Arguments, Does.Contain("plan").And.Not.Contain("acceptEdits"));
    }

    [Test]
    public void PassesTheSettingsThatMakeTheSessionReportWhatItDoes()
    {
        const string Settings = "{\"hooks\":{}}";

        var fresh = _builder.BuildNew(Id, @"C:\wt", Settings);
        var resumed = _builder.BuildResume(Id, @"C:\wt", Settings);

        Assert.That(fresh.Arguments, Is.EqualTo(Expect.Seq("--session-id", "6d8f503c-c671-e387-7413-d0d074951f3a", "--permission-mode", "acceptEdits", "--settings", Settings)));
        Assert.That(resumed.Arguments, Is.EqualTo(Expect.Seq("--resume", "6d8f503c-c671-e387-7413-d0d074951f3a", "--permission-mode", "acceptEdits", "--settings", Settings)));
    }

    [Test]
    public void PassesNoSettingsWhenThereAreNone()
    {
        Assert.That(_builder.BuildNew(Id, @"C:\wt").Arguments, Does.Not.Contain("--settings"));
        Assert.That(_builder.BuildResume(Id, @"C:\wt", settings: null).Arguments, Does.Not.Contain("--settings"));
    }

    [Test]
    public void RefusesAMissingWorktreePath()
    {
        Assert.That(() => _builder.BuildNew(Id, " "), Throws.ArgumentException);
        Assert.That(() => _builder.BuildResume(Id, " "), Throws.ArgumentException);
    }
}
