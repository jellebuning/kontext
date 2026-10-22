using System.Net.Http.Headers;
using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.GitHub;

internal sealed class GitHubDeviceLogin(GitHubOptions options, HttpClient http, TimeProvider time) : IGitHubDeviceLogin
{
    private const string DeviceCodeUrl = "https://github.com/login/device/code";
    private const string AccessTokenUrl = "https://github.com/login/oauth/access_token";
    private const string DeviceGrantType = "urn:ietf:params:oauth:grant-type:device_code";
    private const string Scope = "repo";
    private const string NotConfigured = "GitHub sign-in is not configured. Set the KONTEXT_GITHUB_CLIENT_ID environment variable to the client id of a GitHub OAuth App with device flow enabled, then restart Kontext.";

    private static readonly TimeSpan SlowDownStep = TimeSpan.FromSeconds(5);

    public async Task<DeviceCodeChallenge> StartAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            throw new GitHubException(NotConfigured);
        }

        var body = await PostAsync(
            DeviceCodeUrl,
            new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId,
                ["scope"] = Scope,
            },
            cancellationToken);

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

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            throw new GitHubException(NotConfigured);
        }

        var interval = challenge.PollInterval;
        while (true)
        {
            await Task.Delay(interval, time, cancellationToken);

            if (time.GetUtcNow() >= challenge.ExpiresAt)
            {
                throw Expired();
            }

            var body = await PostAsync(
                AccessTokenUrl,
                new Dictionary<string, string>
                {
                    ["client_id"] = options.ClientId,
                    ["device_code"] = challenge.DeviceCode,
                    ["grant_type"] = DeviceGrantType,
                },
                cancellationToken);

            var result = DeviceFlowResponses.ParseTokenPoll(body);
            switch (result.Status)
            {
                case DeviceTokenStatus.Granted:
                    return result.Token!;
                case DeviceTokenStatus.Pending:
                    break;
                case DeviceTokenStatus.SlowDown:
                    interval = result.IntervalSeconds is { } seconds && seconds > 0
                        ? TimeSpan.FromSeconds(seconds)
                        : interval + SlowDownStep;
                    break;
                case DeviceTokenStatus.Expired:
                    throw Expired();
                case DeviceTokenStatus.Denied:
                    throw new AuthenticationFailedException("Sign-in was cancelled on GitHub.");
                default:
                    throw new GitHubException($"{DeviceFlowResponses.Describe(result.Error ?? "unknown", result.Description)} Check the client id and that device flow is enabled for the GitHub OAuth App.");
            }
        }
    }

    private static AuthenticationFailedException Expired() => new("The sign-in code expired. Please try again.");

    private async Task<string> PostAsync(string url, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Kontext", "1.0"));

            using var response = await http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode && !body.TrimStart().StartsWith('{'))
            {
                throw new GitHubException($"GitHub returned an error (HTTP {(int)response.StatusCode}) while signing in.");
            }

            return body;
        }
        catch (HttpRequestException ex)
        {
            throw new GitHubException("Could not reach GitHub. Check your internet connection.", ex);
        }
    }
}
