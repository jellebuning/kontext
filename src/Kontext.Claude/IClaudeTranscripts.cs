using Kontext.Domain;

namespace Kontext.Claude;

internal interface IClaudeTranscripts
{
    bool Exists(TerminalSessionId sessionId);
}
