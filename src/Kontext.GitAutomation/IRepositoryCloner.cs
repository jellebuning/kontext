using Kontext.Kernel;

namespace Kontext.GitAutomation;

public interface IRepositoryCloner
{
    Task<string> CloneAsync(RepositoryRef repository, CancellationToken cancellationToken = default);
}
