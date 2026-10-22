using System.Text.Json;
using Kontext.Domain;

namespace Kontext.Claude.Tests;

[TestFixture]
public class ClaudeHookSettingsBuilderTests
{
    private static readonly TerminalSessionId Session = new(Guid.Parse("6d8f503c-c671-e387-7413-d0d074951f3a"));
    private static readonly Uri Address = new("http://127.0.0.1:50123/");

    private static JsonElement Build() => JsonDocument.Parse(ClaudeHookSettingsBuilder.Build(Address, Session)).RootElement.GetProperty("hooks");

    [Test]
    public void PostsEveryEventToTheAddressOfThisSession()
    {
        var hooks = Build();

        foreach (var name in new[] { "UserPromptSubmit", "PreToolUse", "PostToolUse", "PostToolUseFailure", "PermissionRequest", "Notification", "Stop", "StopFailure" })
        {
            var hook = hooks.GetProperty(name)[0].GetProperty("hooks")[0];
            Assert.That(hook.GetProperty("type").GetString(), Is.EqualTo("http"), name);
            Assert.That(hook.GetProperty("url").GetString(), Is.EqualTo("http://127.0.0.1:50123/hook/6d8f503c-c671-e387-7413-d0d074951f3a"), name);
        }
    }

    [Test]
    public void GivesEachSessionItsOwnAddressSoEventsNeverMixAndNeedNoClaudeSessionNumbers()
    {
        var other = ClaudeHookSettingsBuilder.Build(Address, TerminalSessionId.New());

        Assert.That(other, Is.Not.EqualTo(ClaudeHookSettingsBuilder.Build(Address, Session)));
    }

    [Test]
    public void NeverWaitsLongForKontextBecauseClaudeIsBlockedUntilItAnswers()
    {
        var timeout = Build().GetProperty("Stop")[0].GetProperty("hooks")[0].GetProperty("timeout").GetInt32();

        Assert.That(timeout, Is.InRange(1, 10));
    }

    [Test]
    public void DoesNotWatchEventsKontextHasNoUseFor()
    {
        var names = Build().EnumerateObject().Select(property => property.Name).ToList();

        Assert.That(names, Does.Not.Contain("SessionStart").And.Not.Contain("SessionEnd"));
    }
}
