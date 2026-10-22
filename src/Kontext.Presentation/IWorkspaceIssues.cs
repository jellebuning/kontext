using Kontext.Domain;

namespace Kontext.Presentation;

public interface IWorkspaceIssues
{
    Task<IReadOnlyList<GitHubIssue>> FindMissingAsync(
        IReadOnlyList<GitHubIssue> shown,
        IReadOnlyList<ExistingWorkspace> workspaces,
        CancellationToken cancellationToken = default);
}
