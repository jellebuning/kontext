using Kontext.Domain;

namespace Kontext.Claude.UnitTests;
internal sealed class FakeClaudeHooks : IClaudeHooks
{
    public const string DefaultSettings = "{\"hooks\":{}}";

    public string? Settings { get; set; } = DefaultSettings;

    public List<TerminalSessionId> Asked { get; } = [];

    public string? SettingsFor(TerminalSessionId session)
    {
        Asked.Add(session);
        return Settings;
    }
}

internal sealed class FakeClaudeTranscripts : IClaudeTranscripts
{
    public HashSet<TerminalSessionId> Saved { get; } = [];

    public bool Exists(TerminalSessionId sessionId) => Saved.Contains(sessionId);
}
