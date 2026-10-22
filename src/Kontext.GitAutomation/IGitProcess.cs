
namespace Kontext.GitAutomation;

public interface IGitProcess
{
    Task<GitResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}

public static class GitProcessExtensions
{
    public static async Task<GitResult> RunCheckedAsync(
        this IGitProcess git,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(git);

        var result = await git.RunAsync(workingDirectory, arguments, cancellationToken);
        if (!result.Succeeded)
        {
            var detail = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput.Trim() : result.StandardError.Trim();
            throw new GitAutomationException($"git {string.Join(' ', arguments)} failed: {detail}", result.ExitCode, result.StandardError);
        }

        return result;
    }
}
