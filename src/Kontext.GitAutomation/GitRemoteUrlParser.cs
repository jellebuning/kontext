using System.Text.RegularExpressions;
using Kontext.Kernel;

namespace Kontext.GitAutomation;

internal static partial class GitRemoteUrlParser
{
    public static RepositoryRef? Parse(string remoteUrl)
    {
        var match = UrlForm().Match(remoteUrl.Trim());
        if (!match.Success)
        {
            match = ScpForm().Match(remoteUrl.Trim());
        }

        return match.Success ? new RepositoryRef(match.Groups["owner"].Value, match.Groups["name"].Value) : null;
    }

    [GeneratedRegex(@"^(?:https?|git|ssh)://(?:[^@/]+@)?(?:www\.)?github\.com(?::\d+)?/(?<owner>[^/]+)/(?<name>[^/]+?)(?:\.git)?/?$", RegexOptions.IgnoreCase)]
    private static partial Regex UrlForm();

    [GeneratedRegex(@"^(?:[^@/]+@)?github\.com:(?<owner>[^/]+)/(?<name>[^/]+?)(?:\.git)?/?$", RegexOptions.IgnoreCase)]
    private static partial Regex ScpForm();
}
