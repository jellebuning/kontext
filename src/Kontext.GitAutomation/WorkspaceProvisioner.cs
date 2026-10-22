using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.GitAutomation;

internal sealed class WorkspaceProvisioner(
    IRepositoryCloneResolver cloneResolver,
    IBranchNamer branchNamer,
    IGitWorktreeService worktrees) : IWorkspaceProvisioner
{
    public async Task<WorktreeInfo> ProvisionAsync(GitHubIssue issue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);

        var repository = await cloneResolver.ResolveAsync(issue.Repository, cancellationToken);

        if (await FindExistingAsync(repository, issue, cancellationToken) is { } existing)
        {
            return existing;
        }

        return await worktrees.CreateAsync(new WorktreeRequest(repository, branchNamer.Name(issue)), cancellationToken);
    }

    private async Task<WorktreeInfo?> FindExistingAsync(RepositoryRef repository, GitHubIssue issue, CancellationToken cancellationToken)
    {
        var clone = repository.LocalPath
            ?? throw new ArgumentException("The repository has no local clone path.", nameof(repository));

        return (await worktrees.ListAsync(clone, cancellationToken))
            .FirstOrDefault(worktree => !worktree.IsMain && branchNamer.IssueNumberOf(worktree.BranchName) == issue.Number);
    }
}
