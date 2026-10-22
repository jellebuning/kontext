using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.Claude;

internal sealed class ClaudeLauncher(ClaudeLaunchBuilder launch, IClaudeTranscripts transcripts, IClaudeHooks hooks) : IClaudeLauncher
{
    public ClaudeLaunch Prepare(StoredSession? earlier, WorktreeInfo worktree)
    {
        ArgumentNullException.ThrowIfNull(worktree);

        var resumable = earlier is not null
            && SameFolder(earlier.WorktreePath, worktree.Path)
            && transcripts.Exists(earlier.SessionId)
            ? earlier
            : null;

        var id = resumable?.SessionId ?? TerminalSessionId.New();
        var settings = hooks.SettingsFor(id);
        var options = resumable is null
            ? launch.BuildNew(id, worktree.Path, settings)
            : launch.BuildResume(id, worktree.Path, settings);

        return new ClaudeLaunch(id, options, ReportsItself: settings is not null, Resumed: resumable is not null);
    }

    private static bool SameFolder(string first, string second)
        => string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
            StringComparison.OrdinalIgnoreCase);
}
