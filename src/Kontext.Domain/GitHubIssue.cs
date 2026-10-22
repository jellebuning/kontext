using Kontext.Kernel;

namespace Kontext.Domain;

public sealed record GitHubIssue(
    RepositoryRef Repository,
    int Number,
    string Title,
    string? Body,
    string State,
    IReadOnlyList<string> Labels,
    Uri Url,
    string? Author,
    DateTimeOffset UpdatedAt)
{
    public IssueKey Key => new(Repository, Number);

    public bool IsClosed => string.Equals(State, "closed", StringComparison.OrdinalIgnoreCase);
}
