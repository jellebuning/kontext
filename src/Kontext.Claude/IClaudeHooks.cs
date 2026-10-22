using Kontext.Domain;

namespace Kontext.Claude;

internal interface IClaudeHooks
{
    string? SettingsFor(TerminalSessionId session);
}
