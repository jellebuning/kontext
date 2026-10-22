using Kontext.Contracts;
using Kontext.Kernel;

namespace Kontext.GitAutomation;

internal sealed class WorkspaceRemover(
    IRepositoryCloneStore cloneStore,
    IGitWorktreeService worktrees,
    IGitProcess git,
    IFolderRemover folders,
    GitAutomationOptions options) : IWorkspaceRemover
{
    public async Task RemoveAsync(RepositoryRef repository, string worktreePath, string branchName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(worktreePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);

        var clone = cloneStore.Find(repository)
            ?? throw new GitAutomationException($"There is no local clone of {repository.FullName}.");
        var folder = Path.GetFullPath(worktreePath);

        await RemoveRegisteredWorktreeAsync(clone, folder, cancellationToken);
        DeleteWhatIsLeftOf(folder);

        await worktrees.PruneAsync(clone, cancellationToken);
        await DeleteBranchAsync(clone, branchName, cancellationToken);
    }

    private async Task RemoveRegisteredWorktreeAsync(string clone, string folder, CancellationToken cancellationToken)
    {
        var registered = (await worktrees.ListAsync(clone, cancellationToken)).Any(worktree => IsSameFolder(worktree.Path, folder));
        if (!registered)
        {
            return;
        }

        try
        {
            await worktrees.RemoveAsync(folder, force: true, cancellationToken);
        }
        catch (GitAutomationException) when (folders.Exists(folder) && IsManaged(folder))
        {
        }
    }

    private void DeleteWhatIsLeftOf(string folder)
    {
        if (!folders.Exists(folder))
        {
            return;
        }

        if (!IsManaged(folder))
        {
            throw new GitAutomationException($"'{folder}' is outside the folder Kontext keeps its worktrees in and could not be removed by git.");
        }

        folders.Delete(folder);
    }

    private async Task DeleteBranchAsync(string clone, string branchName, CancellationToken cancellationToken)
    {
        var branchExists = (await git.RunAsync(clone, ["show-ref", "--verify", "--quiet", $"refs/heads/{branchName}"], cancellationToken)).Succeeded;
        if (branchExists)
        {
            await git.RunCheckedAsync(clone, ["branch", "-D", branchName], cancellationToken);
        }
    }

    private bool IsManaged(string folder)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(options.WorktreesRoot), folder);
        return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }

    private static bool IsSameFolder(string first, string second)
        => string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
            Path.TrimEndingDirectorySeparator(second),
            StringComparison.OrdinalIgnoreCase);
}
