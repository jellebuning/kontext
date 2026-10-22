namespace Kontext.Kernel;

public sealed record RepositoryRef(string Owner, string Name, string? LocalPath = null)
{
    public string FullName => $"{Owner}/{Name}";
}
