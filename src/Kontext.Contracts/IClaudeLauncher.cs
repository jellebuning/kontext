using Kontext.Domain;

namespace Kontext.Contracts;

public interface IClaudeLauncher
{
    ClaudeLaunch Prepare(StoredSession? earlier, WorktreeInfo worktree);
}
