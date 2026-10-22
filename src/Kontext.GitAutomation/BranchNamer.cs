using System.Globalization;
using System.Text;
using Kontext.Domain;

namespace Kontext.GitAutomation;

internal sealed class BranchNamer : IBranchNamer
{
    private const string Prefix = "features/";
    private const string LegacyPrefix = "kontext/";
    private const int MaxSlugLength = 40;

    private static readonly string[] KnownPrefixes = [Prefix, LegacyPrefix];

    public string Name(GitHubIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);

        var slug = Slugify(issue.Title);
        return slug.Length == 0
            ? $"{Prefix}{issue.Number}"
            : $"{Prefix}{issue.Number}-{slug}";
    }

    public int? IssueNumberOf(string branchName)
    {
        ArgumentNullException.ThrowIfNull(branchName);

        var prefix = KnownPrefixes.FirstOrDefault(known => branchName.StartsWith(known, StringComparison.Ordinal));
        if (prefix is null)
        {
            return null;
        }

        var rest = branchName.AsSpan(prefix.Length);
        var digits = 0;
        while (digits < rest.Length && char.IsAsciiDigit(rest[digits]))
        {
            digits++;
        }

        var endsCleanly = digits == rest.Length || rest[digits] == '-';
        return digits > 0 && endsCleanly && int.TryParse(rest[..digits], out var number) ? number : null;
    }

    private static string Slugify(string title)
    {
        var builder = new StringBuilder();

        foreach (var character in title.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(character);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(lower);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length <= MaxSlugLength ? slug : slug[..MaxSlugLength].TrimEnd('-');
    }
}
