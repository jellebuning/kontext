using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Presentation;

internal sealed class WorkspaceIssues(IGitHubService gitHub) : IWorkspaceIssues
{
    public async Task<IReadOnlyList<GitHubIssue>> FindMissingAsync(
        IReadOnlyList<GitHubIssue> shown,
        IReadOnlyList<ExistingWorkspace> workspaces,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(workspaces);

        var known = shown.Select(issue => issue.Key).ToHashSet();
        var missing = workspaces.Where(workspace => !known.Contains(workspace.Issue)).DistinctBy(workspace => workspace.Issue).ToList();

        var fetched = await Task.WhenAll(missing.Select(workspace => FetchAsync(workspace, cancellationToken)));

        return fetched.OfType<GitHubIssue>().ToList();
    }

    private async Task<GitHubIssue?> FetchAsync(ExistingWorkspace workspace, CancellationToken cancellationToken)
    {
        try
        {
            return await gitHub.GetIssueAsync(workspace.Repository, workspace.Number, cancellationToken);
        }
        catch (KontextException)
        {
            return null;
        }
    }
}
