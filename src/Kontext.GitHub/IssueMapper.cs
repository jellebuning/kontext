using Kontext.Domain;
using Kontext.Kernel;
using Octokit;

namespace Kontext.GitHub;

internal static class IssueMapper
{
    public static GitHubIssue? Map(Issue issue, RepositoryRef? requestedRepository)
    {
        if (issue.PullRequest is not null)
        {
            return null;
        }

        var repository = issue.Repository is { } source
            ? new RepositoryRef(source.Owner.Login, source.Name)
            : requestedRepository ?? RepositoryFromIssueUrl(issue.HtmlUrl);

        return new GitHubIssue(
            repository,
            issue.Number,
            issue.Title,
            issue.Body,
            issue.State.StringValue,
            (issue.Labels ?? []).Select(label => label.Name).ToList(),
            new Uri(issue.HtmlUrl),
            issue.User?.Login,
            issue.UpdatedAt ?? issue.CreatedAt);
    }

    private static RepositoryRef RepositoryFromIssueUrl(string htmlUrl)
    {
        var segments = new Uri(htmlUrl).AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return new RepositoryRef(segments[0], segments[1]);
    }
}
