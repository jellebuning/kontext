using Kontext.Kernel;

namespace Kontext.Contracts;

public interface IRepositoryCloneStore
{
    string? Find(RepositoryRef repository);

    void Save(RepositoryRef repository, string localPath);

    IReadOnlyList<RepositoryRef> List();
}
