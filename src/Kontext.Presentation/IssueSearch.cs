using System.Globalization;
using Kontext.Domain;

namespace Kontext.Presentation;

public static class IssueSearch
{
    public static bool Matches(GitHubIssue issue, string? query)
    {
        ArgumentNullException.ThrowIfNull(issue);

        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(word => MatchesWord(issue, word));
    }

    private static bool MatchesWord(GitHubIssue issue, string word)
    {
        if (word[0] == '#')
        {
            return issue.Number.ToString(CultureInfo.InvariantCulture).StartsWith(word[1..], StringComparison.Ordinal);
        }

        return Contains(issue.Title, word)
            || Contains(issue.Repository.FullName, word)
            || Contains(issue.Author, word)
            || issue.Labels.Any(label => Contains(label, word));
    }

    private static bool Contains(string? text, string word)
        => text?.Contains(word, StringComparison.OrdinalIgnoreCase) == true;
}
