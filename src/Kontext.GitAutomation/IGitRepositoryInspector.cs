using Kontext.Kernel;

namespace Kontext.GitAutomation;

public interface IGitRepositoryInspector
{
    Task<string?> FindRepositoryRootAsync(string folder, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RepositoryRef>> GetGitHubRemotesAsync(string repositoryRoot, CancellationToken cancellationToken = default);
}
