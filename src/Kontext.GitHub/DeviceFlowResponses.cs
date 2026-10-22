using System.Text.Json;
using Kontext.Domain;

namespace Kontext.GitHub;

internal enum DeviceTokenStatus
{
    Granted,
    Pending,
    SlowDown,
    Expired,
    Denied,
    Failed,
}

internal sealed record DeviceTokenResult(
    DeviceTokenStatus Status,
    string? Token = null,
    int? IntervalSeconds = null,
    string? Error = null,
    string? Description = null);

internal static class DeviceFlowResponses
{
    private const int DefaultIntervalSeconds = 5;

    public static DeviceCodeChallenge ParseDeviceCode(string json, DateTimeOffset now)
    {
        using var document = Parse(json);
        var root = document.RootElement;

        if (Text(root, "error") is { } error)
        {
            throw new GitHubException(Describe(error, Text(root, "error_description")));
        }

        var deviceCode = Text(root, "device_code");
        var userCode = Text(root, "user_code");
        var verificationUri = Text(root, "verification_uri");
        if (string.IsNullOrWhiteSpace(deviceCode)
            || string.IsNullOrWhiteSpace(userCode)
            || !Uri.TryCreate(verificationUri, UriKind.Absolute, out var uri)
            || !root.TryGetProperty("expires_in", out var expiresIn)
            || !expiresIn.TryGetInt32(out var expiresInSeconds))
        {
            throw Unexpected();
        }

        var interval = root.TryGetProperty("interval", out var intervalValue) && intervalValue.TryGetInt32(out var seconds) && seconds > 0
            ? seconds
            : DefaultIntervalSeconds;

        return new DeviceCodeChallenge(deviceCode, userCode, uri, now.AddSeconds(expiresInSeconds), TimeSpan.FromSeconds(interval));
    }

    public static DeviceTokenResult ParseTokenPoll(string json)
    {
        using var document = Parse(json);
        var root = document.RootElement;

        if (Text(root, "access_token") is { Length: > 0 } token)
        {
            return new DeviceTokenResult(DeviceTokenStatus.Granted, Token: token);
        }

        var error = Text(root, "error");
        var description = Text(root, "error_description");
        return error switch
        {
            "authorization_pending" => new DeviceTokenResult(DeviceTokenStatus.Pending),
            "slow_down" => new DeviceTokenResult(
                DeviceTokenStatus.SlowDown,
                IntervalSeconds: root.TryGetProperty("interval", out var interval) && interval.TryGetInt32(out var seconds) ? seconds : null),
            "expired_token" => new DeviceTokenResult(DeviceTokenStatus.Expired),
            "access_denied" => new DeviceTokenResult(DeviceTokenStatus.Denied),
            null => throw Unexpected(),
            _ => new DeviceTokenResult(DeviceTokenStatus.Failed, Error: error, Description: description),
        };
    }

    public static string Describe(string error, string? description) =>
        string.IsNullOrWhiteSpace(description) ? $"GitHub sign-in failed ({error})." : $"GitHub sign-in failed: {description}";

    private static JsonDocument Parse(string json)
    {
        try
        {
            var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                throw Unexpected();
            }

            return document;
        }
        catch (JsonException ex)
        {
            throw new GitHubException("GitHub sent a response Kontext could not read.", ex);
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static GitHubException Unexpected() => new("GitHub sent a response Kontext could not read.");
}
