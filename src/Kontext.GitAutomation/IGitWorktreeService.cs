using Kontext.Domain;

namespace Kontext.GitAutomation;

public interface IGitWorktreeService
{
    Task<WorktreeInfo> CreateAsync(WorktreeRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorktreeInfo>> ListAsync(string repositoryPath, CancellationToken cancellationToken = default);

    Task RemoveAsync(string worktreePath, bool force = false, CancellationToken cancellationToken = default);

    Task PruneAsync(string repositoryPath, CancellationToken cancellationToken = default);
}
