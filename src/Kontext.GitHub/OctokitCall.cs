using Kontext.Domain;

namespace Kontext.GitHub;

internal static class OctokitCall
{
    public static async Task<T> RunAsync<T>(Func<Task<T>> call, CancellationToken cancellationToken)
    {
        try
        {
            return await call().WaitAsync(cancellationToken);
        }
        catch (Octokit.AuthorizationException ex)
        {
            throw new AuthenticationFailedException("GitHub rejected this sign-in. It may have expired or been revoked.", ex);
        }
        catch (Octokit.ApiException ex)
        {
            throw new GitHubException($"GitHub returned an error: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new GitHubException("Could not reach GitHub. Check your internet connection.", ex);
        }
    }
}
