using Kontext.Kernel;

namespace Kontext.GitAutomation;

internal sealed class GitRepositoryInspector(IGitProcess git) : IGitRepositoryInspector
{
    public async Task<string?> FindRepositoryRootAsync(string folder, CancellationToken cancellationToken = default)
    {
        var result = await git.RunAsync(folder, ["rev-parse", "--show-toplevel"], cancellationToken);
        return result.Succeeded && result.Output.Length > 0 ? Path.GetFullPath(result.Output) : null;
    }

    public async Task<IReadOnlyList<RepositoryRef>> GetGitHubRemotesAsync(string repositoryRoot, CancellationToken cancellationToken = default)
    {
        var result = await git.RunAsync(repositoryRoot, ["config", "--get-regexp", @"^remote\..*\.url$"], cancellationToken);
        if (!result.Succeeded)
        {
            return [];
        }

        return result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line[(line.IndexOf(' ', StringComparison.Ordinal) + 1)..])
            .Select(GitRemoteUrlParser.Parse)
            .OfType<RepositoryRef>()
            .Distinct()
            .ToList();
    }
}
