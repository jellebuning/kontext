namespace Kontext.Kernel;

public readonly record struct IssueKey
{
    public IssueKey(string repositoryFullName, int number)
    {
        RepositoryFullName = repositoryFullName.ToUpperInvariant();
        Number = number;
    }

    public IssueKey(RepositoryRef repository, int number)
        : this(repository.FullName, number)
    {
    }

    public string RepositoryFullName { get; }

    public int Number { get; }

    public override string ToString() => $"{RepositoryFullName}#{Number}";
}
