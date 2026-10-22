namespace Kontext.Presentation;

public sealed record IssueGroup<T>(string Repository, IReadOnlyList<T> Items);

public static class IssueGrouper
{
    public static IReadOnlyList<IssueGroup<T>> Group<T>(
        IEnumerable<T> items,
        Func<T, string> repositoryOf,
        Func<T, DateTimeOffset> updatedOf)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(repositoryOf);
        ArgumentNullException.ThrowIfNull(updatedOf);

        return items
            .GroupBy(repositoryOf, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new IssueGroup<T>(group.Key, group.OrderByDescending(updatedOf).ToList()))
            .ToList();
    }
}
