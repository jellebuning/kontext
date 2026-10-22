using Kontext.Kernel;

namespace Kontext.GitAutomation;

internal sealed class WorktreePathProvider(GitAutomationOptions options) : IWorktreePathProvider
{
    public string GetPath(RepositoryRef repository, string branchName)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);

        var folder = branchName[(branchName.LastIndexOf('/') + 1)..];

        return Path.Combine(options.WorktreesRoot, Safe(repository.Owner), Safe(repository.Name), Safe(folder));
    }

    private static string Safe(string segment)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(segment.Select(character => invalid.Contains(character) ? '_' : character));
    }
}
