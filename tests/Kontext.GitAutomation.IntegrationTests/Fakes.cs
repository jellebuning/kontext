using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.GitAutomation.IntegrationTests;

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

internal sealed class NoGitAuthentication : IGitAuthentication
{
    public IReadOnlyDictionary<string, string> EnvironmentVariables() => new Dictionary<string, string>();
}

internal sealed class FakeCloneResolver(RepositoryRef answer) : IRepositoryCloneResolver
{
    public Task<RepositoryRef> ResolveAsync(RepositoryRef repository, CancellationToken cancellationToken = default)
        => Task.FromResult(answer);
}
