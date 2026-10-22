using Kontext.Kernel;

namespace Kontext.Contracts;

public interface IWorkspaceRemover
{
    Task RemoveAsync(RepositoryRef repository, string worktreePath, string branchName, CancellationToken cancellationToken = default);
}
