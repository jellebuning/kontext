using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.GitAutomation.UnitTests;

internal sealed class FakeCloner(string clonedTo) : IRepositoryCloner
{
    public int Calls { get; private set; }

    public RepositoryRef? Last { get; private set; }

    public Exception? FailWith { get; set; }

    public Task<string> CloneAsync(RepositoryRef repository, CancellationToken cancellationToken = default)
    {
        Calls++;
        Last = repository;
        return FailWith is null ? Task.FromResult(clonedTo) : Task.FromException<string>(FailWith);
    }
}

internal sealed class FakeGitProcess : IGitProcess
{
    public List<(string WorkingDirectory, string[] Arguments)> Runs { get; } = [];

    public GitResult Result { get; set; } = new(0, string.Empty, string.Empty);

    public Task<GitResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        Runs.Add((workingDirectory, [.. arguments]));
        return Task.FromResult(Result);
    }
}

internal sealed class FakeInspector : IGitRepositoryInspector
{
    private readonly Dictionary<string, (string Root, RepositoryRef[] Remotes)> _folders = new(StringComparer.OrdinalIgnoreCase);

    public FakeInspector WithRepository(string folder, string root, params RepositoryRef[] remotes)
    {
        _folders[folder] = (root, remotes);
        return this;
    }

    public Task<string?> FindRepositoryRootAsync(string folder, CancellationToken cancellationToken = default)
        => Task.FromResult(_folders.TryGetValue(folder, out var entry) ? entry.Root : null);

    public Task<IReadOnlyList<RepositoryRef>> GetGitHubRemotesAsync(string repositoryRoot, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RepositoryRef>>(
            _folders.Values.FirstOrDefault(entry => entry.Root == repositoryRoot).Remotes ?? []);
}

internal sealed class FakeCloneResolver(RepositoryRef answer) : IRepositoryCloneResolver
{
    public Task<RepositoryRef> ResolveAsync(RepositoryRef repository, CancellationToken cancellationToken = default)
        => Task.FromResult(answer);
}

internal sealed class FakeBranchNamer(string branch, int? issueNumber = null) : IBranchNamer
{
    public string Name(GitHubIssue issue) => branch;

    public int? IssueNumberOf(string branchName) => issueNumber;
}

internal sealed class FakeWorktreeService : IGitWorktreeService
{
    private readonly Dictionary<string, IReadOnlyList<WorktreeInfo>> _listings = new(StringComparer.OrdinalIgnoreCase);

    public WorktreeRequest? LastRequest { get; private set; }

    public List<string> ListedPaths { get; } = [];

    public FakeWorktreeService WithListing(string repositoryPath, params WorktreeInfo[] worktrees)
    {
        _listings[repositoryPath] = worktrees;
        return this;
    }

    public Task<WorktreeInfo> CreateAsync(WorktreeRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult(new WorktreeInfo(@"C:\wt", request.BranchName, "abc", IsMain: false));
    }

    public Task<IReadOnlyList<WorktreeInfo>> ListAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        ListedPaths.Add(repositoryPath);

        return _listings.TryGetValue(repositoryPath, out var listing)
            ? Task.FromResult(listing)
            : Task.FromException<IReadOnlyList<WorktreeInfo>>(new GitAutomationException($"cannot read {repositoryPath}"));
    }

    public List<(string Path, bool Force)> Removed { get; } = [];

    public List<string> Pruned { get; } = [];

    public Exception? RemoveFailsWith { get; set; }

    public Task RemoveAsync(string worktreePath, bool force = false, CancellationToken cancellationToken = default)
    {
        Removed.Add((worktreePath, force));
        return RemoveFailsWith is null ? Task.CompletedTask : Task.FromException(RemoveFailsWith);
    }

    public Task PruneAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        Pruned.Add(repositoryPath);
        return Task.CompletedTask;
    }
}

internal static class TestIssues
{
    public static GitHubIssue Create(int number = 7, string title = "Fix login crash", string owner = "acme", string name = "app")
        => new(new RepositoryRef(owner, name), number, title, Body: null, State: "open", Labels: [], new Uri($"https://github.com/{owner}/{name}/issues/{number}"), Author: "someone", UpdatedAt: DateTimeOffset.UnixEpoch);
}

internal static class Expect
{
    public static T[] Seq<T>(params T[] items) => items;
}

internal sealed class FakeFolderRemover : IFolderRemover
{
    public HashSet<string> Existing { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Deleted { get; } = [];

    public Exception? FailWith { get; set; }

    public bool Exists(string path) => Existing.Contains(path);

    public void Delete(string path)
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }

        Deleted.Add(path);
        Existing.Remove(path);
    }
}
