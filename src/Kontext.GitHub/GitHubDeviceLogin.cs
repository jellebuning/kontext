using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.GitHub;

internal sealed class GitHubDeviceLogin(GitHubOptions options, GitHubFormPoster poster, TimeProvider time) : IGitHubDeviceLogin
{
    private const string DeviceCodeUrl = "https://github.com/login/device/code";
    private const string AccessTokenUrl = "https://github.com/login/oauth/access_token";
    private const string DeviceGrantType = "urn:ietf:params:oauth:grant-type:device_code";
    private const string NotConfigured = "GitHub sign-in is not configured. Set the KONTEXT_GITHUB_CLIENT_ID environment variable to the client id of a GitHub OAuth App with device flow enabled, then restart Kontext.";

    private static readonly TimeSpan SlowDownStep = TimeSpan.FromSeconds(5);

    public async Task<DeviceCodeChallenge> StartAsync(CancellationToken cancellationToken = default)
    {
        var clientId = RequireClientId();
        var body = await poster.PostAsync(DeviceCodeUrl, new Dictionary<string, string> { ["client_id"] = clientId, ["scope"] = "repo" }, cancellationToken);

        var challenge = DeviceFlowResponses.ParseDeviceCode(body, time.GetUtcNow());
        if (challenge.VerificationUri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(challenge.VerificationUri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new GitHubException("GitHub sent an unexpected verification address, so the sign-in was stopped.");
        }

        return challenge;
    }

    public async Task<string> WaitForAccessTokenAsync(DeviceCodeChallenge challenge, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        var clientId = RequireClientId();

        var interval = challenge.PollInterval;
        while (true)
        {
            await Task.Delay(interval, time, cancellationToken);
            if (time.GetUtcNow() >= challenge.ExpiresAt)
            {
                throw Expired();
            }

            var form = new Dictionary<string, string> { ["client_id"] = clientId, ["device_code"] = challenge.DeviceCode, ["grant_type"] = DeviceGrantType };
            var result = DeviceFlowResponses.ParseTokenPoll(await poster.PostAsync(AccessTokenUrl, form, cancellationToken));
            switch (result.Status)
            {
                case DeviceTokenStatus.Granted:
                    return result.Token!;
                case DeviceTokenStatus.Pending:
                    break;
                case DeviceTokenStatus.SlowDown:
                    interval = result.IntervalSeconds is > 0 and var seconds ? TimeSpan.FromSeconds(seconds) : interval + SlowDownStep;
                    break;
                case DeviceTokenStatus.Expired:
                    throw Expired();
                case DeviceTokenStatus.Denied:
                    throw new AuthenticationFailedException("Sign-in was cancelled on GitHub.");
                default:
                    throw new GitHubException($"{result.Message} Check the client id and that device flow is enabled for the GitHub OAuth App.");
            }
        }
    }

    private static AuthenticationFailedException Expired() => new("The sign-in code expired. Please try again.");

    private string RequireClientId() =>
        string.IsNullOrWhiteSpace(options.ClientId) ? throw new GitHubException(NotConfigured) : options.ClientId;
}
