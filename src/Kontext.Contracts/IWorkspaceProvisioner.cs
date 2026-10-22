using Kontext.Domain;

namespace Kontext.Contracts;

public interface IWorkspaceProvisioner
{
    Task<WorktreeInfo> ProvisionAsync(GitHubIssue issue, CancellationToken cancellationToken = default);
}
