using System.Text;
using Kontext.Contracts;

namespace Kontext.GitAutomation;

internal sealed class GitHubTokenAuthentication(IGitHubTokenStore tokens) : IGitAuthentication
{
    public IReadOnlyDictionary<string, string> EnvironmentVariables()
    {
        var token = tokens.Load();
        if (string.IsNullOrWhiteSpace(token))
        {
            return new Dictionary<string, string>();
        }

        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"x-access-token:{token}"));

        return new Dictionary<string, string>
        {
            ["GIT_CONFIG_COUNT"] = "2",
            ["GIT_CONFIG_KEY_0"] = "http.https://github.com/.extraheader",
            ["GIT_CONFIG_VALUE_0"] = $"AUTHORIZATION: basic {basic}",

            ["GIT_CONFIG_KEY_1"] = "credential.helper",
            ["GIT_CONFIG_VALUE_1"] = string.Empty,
        };
    }
}
