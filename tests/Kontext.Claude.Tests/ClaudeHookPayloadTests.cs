
namespace Kontext.Claude.Tests;

[TestFixture]
public class ClaudeHookPayloadTests
{
    [Test]
    public void ReadsTheEventName()
    {
        var parsed = ClaudeHookPayload.Parse("""{ "session_id": "abc", "hook_event_name": "Stop", "last_assistant_message": "done" }""");

        Assert.That(parsed, Is.EqualTo(("Stop", (string?)null)));
    }

    [Test]
    public void ReadsTheNotificationType()
    {
        var parsed = ClaudeHookPayload.Parse("""{ "hook_event_name": "Notification", "notification_type": "permission_prompt", "message": "Claude needs your permission" }""");

        Assert.That(parsed, Is.EqualTo(("Notification", "permission_prompt")));
    }

    [TestCase("")]
    [TestCase("not json")]
    [TestCase("[1, 2]")]
    [TestCase("\"just a string\"")]
    [TestCase("{}")]
    [TestCase("""{ "hook_event_name": 7 }""")]
    [TestCase("""{ "hook_event_name": "  " }""")]
    public void RejectsAnythingThatIsNotAHookEvent(string body)
    {
        Assert.That(ClaudeHookPayload.Parse(body), Is.Null);
    }
}
