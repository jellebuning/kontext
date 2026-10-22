using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.TestSupport;

public static class Issues
{
    public static GitHubIssue Create(
        int number = 1,
        string title = "A title",
        string repository = "acme/app",
        string[]? labels = null,
        string? author = "octocat",
        DateTimeOffset? updatedAt = null,
        string? body = "A body")
    {
        var parts = repository.Split('/');
        var repo = new RepositoryRef(parts[0], parts[1]);
        return new GitHubIssue(
            repo,
            number,
            title,
            body,
            "open",
            labels ?? [],
            new Uri($"https://github.com/{repository}/issues/{number}"),
            author,
            updatedAt ?? DateTimeOffset.Parse("2025-03-01T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
    }
}

public sealed class FakeGitHubService : IGitHubService
{
    private readonly List<TaskCompletionSource<IReadOnlyList<GitHubIssue>>> _pending = [];

    public IReadOnlyList<GitHubIssue> Issues { get; set; } = [];

    public List<GitHubIssue> ClosedIssues { get; } = [];

    public Exception? FailWith { get; set; }

    public bool Deferred { get; set; }

    public int Calls { get; private set; }

    public GitHubUser? CurrentUser => null;

    public void Complete(int callIndex, IReadOnlyList<GitHubIssue> issues) => _pending[callIndex].SetResult(issues);

    public Task<IReadOnlyList<GitHubIssue>> GetIssuesAsync(RepositoryRef? repository = null, CancellationToken cancellationToken = default)
    {
        Calls++;

        if (FailWith is not null)
        {
            return Task.FromException<IReadOnlyList<GitHubIssue>>(FailWith);
        }

        if (!Deferred)
        {
            return Task.FromResult(Issues);
        }

        var source = new TaskCompletionSource<IReadOnlyList<GitHubIssue>>();
        _pending.Add(source);
        return source.Task;
    }

    public GitHubUser SignedInAs { get; set; } = new("octocat", "The Octocat", new Uri("https://github.com/octocat"));

    public Exception? AuthenticationFailsWith { get; set; }

    public List<string> TokensTried { get; } = [];

    public int SignOuts { get; private set; }

    public Task<GitHubUser> AuthenticateAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        TokensTried.Add(accessToken);
        return AuthenticationFailsWith is null ? Task.FromResult(SignedInAs) : Task.FromException<GitHubUser>(AuthenticationFailsWith);
    }

    public void SignOut() => SignOuts++;


    public Task<GitHubIssue> GetIssueAsync(RepositoryRef repository, int number, CancellationToken cancellationToken = default)
    {
        var found = ClosedIssues.Concat(Issues).FirstOrDefault(issue => issue.Number == number && string.Equals(issue.Repository.FullName, repository.FullName, StringComparison.OrdinalIgnoreCase));
        return found is null ? Task.FromException<GitHubIssue>(new TestKontextException("not found")) : Task.FromResult(found);
    }
}

public sealed class FakeTokenStore(string? saved = null) : IGitHubTokenStore
{
    public string? Saved { get; private set; } = saved;

    public int Clears { get; private set; }

    public Exception? LoadFailsWith { get; set; }

    public string? Load() => LoadFailsWith is null ? Saved : throw LoadFailsWith;

    public void Save(string token) => Saved = token;

    public void Clear()
    {
        Saved = null;
        Clears++;
    }
}
