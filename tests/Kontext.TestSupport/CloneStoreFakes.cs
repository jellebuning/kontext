using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.TestSupport;

public sealed class FakeCloneStore : IRepositoryCloneStore
{
    public Dictionary<string, string> Saved { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? Find(RepositoryRef repository) => Saved.GetValueOrDefault(repository.FullName);

    public void Save(RepositoryRef repository, string localPath) => Saved[repository.FullName] = localPath;

    public IReadOnlyList<RepositoryRef> List()
        => Saved.Keys.Select(name => new RepositoryRef(name.Split('/')[0], name.Split('/')[1])).ToList();
}
