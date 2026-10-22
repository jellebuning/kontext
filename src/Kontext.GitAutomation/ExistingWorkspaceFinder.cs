using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.GitAutomation;

internal sealed class ExistingWorkspaceFinder(
    IRepositoryCloneStore cloneStore,
    IGitWorktreeService worktrees,
    IBranchNamer branchNamer) : IExistingWorkspaceFinder
{
    public async Task<IReadOnlyList<ExistingWorkspace>> FindAllAsync(CancellationToken cancellationToken = default)
    {
        var perRepository = await Task.WhenAll(cloneStore.List().Select(repository => FindInAsync(repository, cancellationToken)));

        return perRepository.SelectMany(found => found).ToList();
    }

    private async Task<IReadOnlyList<ExistingWorkspace>> FindInAsync(RepositoryRef repository, CancellationToken cancellationToken)
    {
        var clone = cloneStore.Find(repository);
        if (clone is null)
        {
            return [];
        }

        IReadOnlyList<WorktreeInfo> listed;
        try
        {
            listed = await worktrees.ListAsync(clone, cancellationToken);
        }
        catch (GitAutomationException)
        {
            return [];
        }

        return listed
            .Where(worktree => !worktree.IsMain)
            .Select(worktree => (Worktree: worktree, Number: branchNamer.IssueNumberOf(worktree.BranchName)))
            .Where(candidate => candidate.Number is not null)
            .Select(candidate => new ExistingWorkspace(repository, candidate.Number!.Value, candidate.Worktree))
            .ToList();
    }
}
