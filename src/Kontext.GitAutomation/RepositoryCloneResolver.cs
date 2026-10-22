using Kontext.Contracts;
using Kontext.Kernel;

namespace Kontext.GitAutomation;

internal sealed class RepositoryCloneResolver(
    IRepositoryCloneStore store,
    IRepositoryCloner cloner,
    IGitRepositoryInspector inspector) : IRepositoryCloneResolver
{
    public async Task<RepositoryRef> ResolveAsync(RepositoryRef repository, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);

        var saved = store.Find(repository);
        if (saved is not null)
        {
            var savedRoot = await inspector.FindRepositoryRootAsync(saved, cancellationToken);
            if (savedRoot is not null)
            {
                return repository with { LocalPath = savedRoot };
            }
        }

        var cloned = await cloner.CloneAsync(repository, cancellationToken);

        var root = await inspector.FindRepositoryRootAsync(cloned, cancellationToken)
            ?? throw new GitAutomationException($"'{cloned}' is not inside a git repository.");

        var remotes = await inspector.GetGitHubRemotesAsync(root, cancellationToken);
        if (!remotes.Any(remote => string.Equals(remote.FullName, repository.FullName, StringComparison.OrdinalIgnoreCase)))
        {
            var found = remotes.Count == 0 ? "has no GitHub remote" : $"is a clone of {string.Join(", ", remotes.Select(remote => remote.FullName))}";
            throw new GitAutomationException($"'{root}' {found}, not {repository.FullName}.");
        }

        store.Save(repository, root);
        return repository with { LocalPath = root };
    }
}
