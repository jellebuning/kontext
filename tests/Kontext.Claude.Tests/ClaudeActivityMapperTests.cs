using Kontext.Domain;

namespace Kontext.Claude.Tests;

[TestFixture]
public class ClaudeActivityMapperTests
{
    private static readonly TerminalSessionId Session = TerminalSessionId.New();

    private static IssueWorkStatus? Next(IssueWorkStatus current, string name, string? notificationType = null)
        => ClaudeActivityMapper.Next(current, new ClaudeHookEvent(Session, name, notificationType));

    [TestCase("UserPromptSubmit")]
    [TestCase("PreToolUse")]
    [TestCase("PostToolUse")]
    [TestCase("PostToolUseFailure")]
    public void WorkingEventsMakeTheSessionRunning(string name)
    {
        Assert.That(Next(IssueWorkStatus.Ready, name), Is.EqualTo(IssueWorkStatus.Running));
        Assert.That(Next(IssueWorkStatus.NeedsInput, name), Is.EqualTo(IssueWorkStatus.Running), "a new prompt starts work again");
        Assert.That(Next(IssueWorkStatus.Done, name), Is.EqualTo(IssueWorkStatus.Running), "work on a closed issue is still work");
    }

    [Test]
    public void FinishingAToolEndsAPermissionWait()
    {
        Assert.That(Next(IssueWorkStatus.NeedsInput, "PostToolUse"), Is.EqualTo(IssueWorkStatus.Running));
    }

    [Test]
    public void ARequestForPermissionMeansTheSessionNeedsInput()
    {
        Assert.That(Next(IssueWorkStatus.Running, "PermissionRequest"), Is.EqualTo(IssueWorkStatus.NeedsInput));
    }

    [TestCase("permission_prompt")]
    [TestCase("elicitation_dialog")]
    [TestCase("elicitation_url_dialog")]
    [TestCase("agent_needs_input")]
    public void NotificationsThatWaitForTheUserMeanNeedsInput(string type)
    {
        Assert.That(Next(IssueWorkStatus.Running, "Notification", type), Is.EqualTo(IssueWorkStatus.NeedsInput));
    }

    [Test]
    public void AFailedAnswerNeedsTheUserToTryAgain()
    {
        Assert.That(Next(IssueWorkStatus.Running, "StopFailure"), Is.EqualTo(IssueWorkStatus.NeedsInput));
    }

    [TestCase(IssueWorkStatus.Running)]
    [TestCase(IssueWorkStatus.NeedsInput)]
    public void StopMeansClaudeFinishedItsAnswerAndIsWaitingForTheNextPrompt(IssueWorkStatus current)
    {
        Assert.That(Next(current, "Stop"), Is.EqualTo(IssueWorkStatus.NeedsInput));
    }

    [Test]
    public void BeingIdleForAMinuteEndsWorkingEvenWhenNoStopCameBecauseTheUserInterrupted()
    {
        Assert.That(Next(IssueWorkStatus.Running, "Notification", "idle_prompt"), Is.EqualTo(IssueWorkStatus.NeedsInput));
    }

    [TestCase(IssueWorkStatus.NeedsInput)]
    [TestCase(IssueWorkStatus.Done)]
    [TestCase(IssueWorkStatus.Ready)]
    public void BeingIdleChangesNothingWhenNothingWasRunning(IssueWorkStatus current)
    {
        Assert.That(Next(current, "Notification", "idle_prompt"), Is.Null);
    }

    [TestCase("SessionStart")]
    [TestCase("SessionEnd")]
    [TestCase("SomethingNew")]
    public void IgnoresEventsItDoesNotUnderstand(string name)
    {
        Assert.That(Next(IssueWorkStatus.Ready, name), Is.Null);
    }

    [Test]
    public void IgnoresNotificationsThatAreOnlyInformation()
    {
        Assert.That(Next(IssueWorkStatus.Running, "Notification", "auth_success"), Is.Null);
        Assert.That(Next(IssueWorkStatus.Running, "Notification", null), Is.Null);
    }

    [TestCase(IssueWorkStatus.NotStarted)]
    [TestCase(IssueWorkStatus.Preparing)]
    [TestCase(IssueWorkStatus.Failed)]
    public void NeverOverridesStatesThatBelongToKontext(IssueWorkStatus current)
    {
        Assert.That(Next(current, "UserPromptSubmit"), Is.Null);
        Assert.That(Next(current, "Stop"), Is.Null);
    }
}
