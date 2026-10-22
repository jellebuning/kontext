using Kontext.Kernel;

namespace Kontext.GitHub;

public class GitHubException : KontextException
{
    public GitHubException(string message)
        : base(message)
    {
    }

    public GitHubException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
