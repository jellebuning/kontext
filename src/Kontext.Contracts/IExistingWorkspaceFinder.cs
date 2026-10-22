using Kontext.Domain;

namespace Kontext.Contracts;

public interface IExistingWorkspaceFinder
{
    Task<IReadOnlyList<ExistingWorkspace>> FindAllAsync(CancellationToken cancellationToken = default);
}
