using Kontext.Kernel;

namespace Kontext.Domain;

public sealed record ExistingWorkspace(RepositoryRef Repository, int Number, WorktreeInfo Worktree)
{
    public IssueKey Issue => new(Repository, Number);
}
