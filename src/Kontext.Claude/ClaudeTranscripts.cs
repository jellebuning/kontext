using System.Globalization;
using Kontext.Domain;

namespace Kontext.Claude;

internal sealed class ClaudeTranscripts(ClaudeOptions options) : IClaudeTranscripts
{
    public bool Exists(TerminalSessionId sessionId)
    {
        var projects = Path.Combine(options.ConfigDirectory, "projects");
        if (!Directory.Exists(projects))
        {
            return false;
        }

        var fileName = sessionId.Value.ToString("D", CultureInfo.InvariantCulture) + ".jsonl";
        return Directory.EnumerateFiles(projects, fileName, SearchOption.AllDirectories).Any();
    }
}
