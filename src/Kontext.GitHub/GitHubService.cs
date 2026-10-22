using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;
using Octokit;

namespace Kontext.GitHub;

internal sealed class GitHubService : IGitHubService
{
    private const int PageSize = 100;

    private const int AllRepositoriesPageCount = 10;

    private static readonly ProductHeaderValue ProductHeader = new("Kontext");

    private GitHubClient _client = new(ProductHeader);

    public GitHubUser? CurrentUser { get; private set; }

    public async Task<GitHubUser> AuthenticateAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var candidate = new GitHubClient(ProductHeader) { Credentials = new Credentials(accessToken) };

        var user = await OctokitCall.RunAsync(() => candidate.User.Current(), cancellationToken);

        _client = candidate;
        CurrentUser = new GitHubUser(user.Login, user.Name, new Uri(user.HtmlUrl));
        return CurrentUser;
    }

    public void SignOut()
    {
        _client = new GitHubClient(ProductHeader);
        CurrentUser = null;
    }

    public async Task<IReadOnlyList<GitHubIssue>> GetIssuesAsync(RepositoryRef? repository = null, CancellationToken cancellationToken = default)
    {
        var client = _client;

        var issues = repository is null
            ? await OctokitCall.RunAsync(
                () => client.Issue.GetAllForCurrent(
                    new IssueRequest
                    {
                        Filter = IssueFilter.All,
                        State = ItemStateFilter.Open,
                        SortProperty = IssueSort.Updated,
                        SortDirection = SortDirection.Descending,
                    },
                    new ApiOptions { PageSize = PageSize, PageCount = AllRepositoriesPageCount }),
                cancellationToken)
            : await OctokitCall.RunAsync(
                () => client.Issue.GetAllForRepository(
                    repository.Owner,
                    repository.Name,
                    new RepositoryIssueRequest
                    {
                        State = ItemStateFilter.Open,
                        SortProperty = IssueSort.Updated,
                        SortDirection = SortDirection.Descending,
                    },
                    new ApiOptions { PageSize = PageSize }),
                cancellationToken);

        return issues
            .Select(issue => IssueMapper.Map(issue, repository))
            .OfType<GitHubIssue>()
            .ToList();
    }

    public async Task<GitHubIssue> GetIssueAsync(RepositoryRef repository, int number, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);

        var client = _client;
        var issue = await OctokitCall.RunAsync(() => client.Issue.Get(repository.Owner, repository.Name, number), cancellationToken);

        return IssueMapper.Map(issue, repository)
            ?? throw new GitHubException($"{repository.FullName}#{number} is a pull request, not an issue.");
    }
}
