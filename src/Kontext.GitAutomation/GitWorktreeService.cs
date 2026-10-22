using Kontext.Domain;

namespace Kontext.GitAutomation;

internal sealed class GitWorktreeService(IGitProcess git, IWorktreePathProvider paths) : IGitWorktreeService
{
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(30);

    public async Task<WorktreeInfo> CreateAsync(WorktreeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var clone = request.Repository.LocalPath
            ?? throw new ArgumentException("The repository has no local clone path.", nameof(request));

        await PruneAsync(clone, cancellationToken);

        var existing = (await ListAsync(clone, cancellationToken)).FirstOrDefault(worktree => worktree.BranchName == request.BranchName);
        if (existing is not null)
        {
            return existing;
        }

        var worktreePath = paths.GetPath(request.Repository, request.BranchName);

        await FetchBestEffortAsync(clone, cancellationToken);

        var branchExists = (await git.RunAsync(clone, ["show-ref", "--verify", "--quiet", $"refs/heads/{request.BranchName}"], cancellationToken)).Succeeded;

        if (branchExists)
        {
            await git.RunCheckedAsync(clone, ["worktree", "add", worktreePath, request.BranchName], cancellationToken);
        }
        else
        {
            var baseRef = request.BaseBranch ?? await ResolveDefaultBaseAsync(clone, cancellationToken);
            await git.RunCheckedAsync(clone, ["worktree", "add", "--no-track", "-b", request.BranchName, worktreePath, baseRef], cancellationToken);
        }

        var head = (await git.RunCheckedAsync(worktreePath, ["rev-parse", "HEAD"], cancellationToken)).Output;
        return new WorktreeInfo(Path.GetFullPath(worktreePath), request.BranchName, head, IsMain: false);
    }

    public async Task<IReadOnlyList<WorktreeInfo>> ListAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var result = await git.RunCheckedAsync(repositoryPath, ["worktree", "list", "--porcelain"], cancellationToken);
        return WorktreePorcelainParser.Parse(result.StandardOutput);
    }

    public async Task RemoveAsync(string worktreePath, bool force = false, CancellationToken cancellationToken = default)
    {
        var commonDirectory = (await git.RunCheckedAsync(worktreePath, ["rev-parse", "--path-format=absolute", "--git-common-dir"], cancellationToken)).Output;
        var mainWorkingTree = Path.GetDirectoryName(Path.GetFullPath(commonDirectory))
            ?? throw new GitAutomationException($"Could not find the main repository for '{worktreePath}'.");

        List<string> arguments = ["worktree", "remove"];
        if (force)
        {
            arguments.Add("--force");
        }

        arguments.Add(worktreePath);
        await git.RunCheckedAsync(mainWorkingTree, arguments, cancellationToken);
    }

    public async Task PruneAsync(string repositoryPath, CancellationToken cancellationToken = default)
        => await git.RunCheckedAsync(repositoryPath, ["worktree", "prune"], cancellationToken);

    private async Task FetchBestEffortAsync(string clone, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(FetchTimeout);

        try
        {
            await git.RunAsync(clone, ["fetch", "origin", "--quiet"], timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task<string> ResolveDefaultBaseAsync(string clone, CancellationToken cancellationToken)
    {
        var remoteHead = await git.RunAsync(clone, ["symbolic-ref", "--short", "refs/remotes/origin/HEAD"], cancellationToken);
        return remoteHead.Succeeded && remoteHead.Output.Length > 0 ? remoteHead.Output : "HEAD";
    }
}
