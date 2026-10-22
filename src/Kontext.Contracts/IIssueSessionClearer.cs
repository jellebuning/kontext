using Kontext.Domain;

namespace Kontext.Contracts;

public interface IIssueSessionClearer
{
    Task ClearAsync(GitHubIssue issue, CancellationToken cancellationToken = default);
}
