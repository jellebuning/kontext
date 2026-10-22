using Kontext.Domain;

namespace Kontext.Contracts;

public interface IIssueSessionCoordinator
{
    Task StartOrResumeAsync(GitHubIssue issue, CancellationToken cancellationToken = default);
}
