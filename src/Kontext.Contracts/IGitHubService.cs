using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Contracts;

public interface IGitHubService
{
    GitHubUser? CurrentUser { get; }

    Task<GitHubUser> AuthenticateAsync(string accessToken, CancellationToken cancellationToken = default);

    void SignOut();

    Task<IReadOnlyList<GitHubIssue>> GetIssuesAsync(RepositoryRef? repository = null, CancellationToken cancellationToken = default);

    Task<GitHubIssue> GetIssueAsync(RepositoryRef repository, int number, CancellationToken cancellationToken = default);
}
