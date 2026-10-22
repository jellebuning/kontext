using System.Collections.Concurrent;
using Kontext.Kernel;

namespace Kontext.GitAutomation;

internal sealed class RepositoryCloner(GitAutomationOptions options, IGitProcess git, IFolderRemover folders) : IRepositoryCloner
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> CloneAsync(RepositoryRef repository, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);

        var target = Path.Combine(options.ClonesRoot, repository.Owner, repository.Name);

        var gate = _gates.GetOrAdd(target, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (folders.Exists(Path.Combine(target, ".git")))
            {
                return target;
            }

            if (folders.Exists(target))
            {
                folders.Delete(target);
            }

            var parent = Path.GetDirectoryName(target)!;
            Directory.CreateDirectory(parent);

            await git.RunCheckedAsync(parent, ["clone", $"https://github.com/{repository.Owner}/{repository.Name}.git", target], cancellationToken);
            return target;
        }
        finally
        {
            gate.Release();
        }
    }
}
