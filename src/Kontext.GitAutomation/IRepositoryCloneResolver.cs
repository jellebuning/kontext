using Kontext.Kernel;

namespace Kontext.GitAutomation;

public interface IRepositoryCloneResolver
{
    Task<RepositoryRef> ResolveAsync(RepositoryRef repository, CancellationToken cancellationToken = default);
}
