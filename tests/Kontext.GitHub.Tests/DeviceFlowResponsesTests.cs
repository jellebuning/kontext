using System.Globalization;

namespace Kontext.GitHub.Tests;

[TestFixture]
public class DeviceFlowResponsesTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2025-03-01T10:00:00Z", CultureInfo.InvariantCulture);

    private const string DeviceCodeJson = """
        {
          "device_code": "3584d83530557fdd1f46af8289938c8ef79f9dc5",
          "user_code": "WDJB-MJHT",
          "verification_uri": "https://github.com/login/device",
          "expires_in": 900,
          "interval": 7
        }
        """;

    [Test]
    public void ParsesTheDeviceCodeResponseIntoAChallengeExpiringRelativeToTheTimePassedIn()
    {
        var challenge = DeviceFlowResponses.ParseDeviceCode(DeviceCodeJson, Now);

        Assert.That(challenge.DeviceCode, Is.EqualTo("3584d83530557fdd1f46af8289938c8ef79f9dc5"));
        Assert.That(challenge.UserCode, Is.EqualTo("WDJB-MJHT"));
        Assert.That(challenge.VerificationUri, Is.EqualTo(new Uri("https://github.com/login/device")));
        Assert.That(challenge.PollInterval, Is.EqualTo(TimeSpan.FromSeconds(7)));
        Assert.That(challenge.ExpiresAt, Is.EqualTo(Now.AddSeconds(900)));
    }

    [TestCase("\"interval\": 7", "\"other\": 1", TestName = "FallsBackToFiveSecondsWhenTheDeviceCodeResponseHasNoInterval")]
    [TestCase("\"interval\": 7", "\"interval\": 0", TestName = "FallsBackToFiveSecondsWhenTheIntervalIsZero")]
    [TestCase("\"interval\": 7", "\"interval\": -3", TestName = "FallsBackToFiveSecondsWhenTheIntervalIsNegative")]
    public void FallsBackToFiveSecondsWithoutAUsableInterval(string original, string replacement)
    {
        var challenge = DeviceFlowResponses.ParseDeviceCode(DeviceCodeJson.Replace(original, replacement, StringComparison.Ordinal), Now);

        Assert.That(challenge.PollInterval, Is.EqualTo(TimeSpan.FromSeconds(5)));
    }

    [TestCase("""{ "error": "device_flow_disabled", "error_description": "Device Flow must be explicitly enabled for this App" }""", "GitHub sign-in failed: Device Flow must be explicitly enabled for this App")]
    [TestCase("""{ "error": "incorrect_client_credentials" }""", "GitHub sign-in failed (incorrect_client_credentials).")]
    public void ADeviceCodeResponseCarryingAnErrorThrowsWithTheDescriptionOrTheErrorName(string json, string message)
    {
        var ex = Assert.Throws<GitHubException>(() => DeviceFlowResponses.ParseDeviceCode(json, Now));

        Assert.That(ex.Message, Is.EqualTo(message));
    }

    [TestCase("""{ "user_code": "A", "verification_uri": "https://github.com/login/device", "expires_in": 900 }""", TestName = "NoDeviceCode")]
    [TestCase("""{ "device_code": "d", "verification_uri": "https://github.com/login/device", "expires_in": 900 }""", TestName = "NoUserCode")]
    [TestCase("""{ "device_code": "d", "user_code": "A", "expires_in": 900 }""", TestName = "NoVerificationUri")]
    [TestCase("""{ "device_code": "d", "user_code": "A", "verification_uri": "not a uri", "expires_in": 900 }""", TestName = "VerificationUriNotAbsolute")]
    [TestCase("""{ "device_code": "d", "user_code": "A", "verification_uri": "https://github.com/login/device" }""", TestName = "NoExpiry")]
    [TestCase("""{ "device_code": "", "user_code": "A", "verification_uri": "https://github.com/login/device", "expires_in": 900 }""", TestName = "BlankDeviceCode")]
    [TestCase("not json", TestName = "NotJson")]
    [TestCase("", TestName = "Empty")]
    [TestCase("[1, 2]", TestName = "JsonArray")]
    [TestCase("\"text\"", TestName = "JsonString")]
    public void AnUnreadableDeviceCodeResponseIsRejected(string json)
    {
        Assert.Throws<GitHubException>(() => DeviceFlowResponses.ParseDeviceCode(json, Now));
    }

    [Test]
    public void ParsesAGrantedPollWithTheToken()
    {
        var result = DeviceFlowResponses.ParseTokenPoll("""{ "access_token": "gho_abc", "token_type": "bearer", "scope": "repo" }""");

        Assert.That(result.Status, Is.EqualTo(DeviceTokenStatus.Granted));
        Assert.That(result.Token, Is.EqualTo("gho_abc"));
    }

    [TestCase("""{ "error": "authorization_pending", "error_description": "The authorization request is still pending." }""", "Pending")]
    [TestCase("""{ "error": "expired_token" }""", "Expired")]
    [TestCase("""{ "error": "access_denied" }""", "Denied")]
    public void ParsesAPollThatCarriesOnlyAStatus(string json, string status)
    {
        var result = DeviceFlowResponses.ParseTokenPoll(json);

        Assert.That(result.Status.ToString(), Is.EqualTo(status));
        Assert.That(result.Token, Is.Null);
    }

    [TestCase("""{ "error": "slow_down", "interval": 10 }""", 10)]
    [TestCase("""{ "error": "slow_down" }""", null)]
    public void ParsesASlowDownPollWithItsOptionalInterval(string json, int? seconds)
    {
        var result = DeviceFlowResponses.ParseTokenPoll(json);

        Assert.That(result.Status, Is.EqualTo(DeviceTokenStatus.SlowDown));
        Assert.That(result.IntervalSeconds, Is.EqualTo(seconds));
    }

    [TestCase("device_flow_disabled", "Device Flow must be explicitly enabled for this App", "GitHub sign-in failed: Device Flow must be explicitly enabled for this App")]
    [TestCase("something_new", null, "GitHub sign-in failed (something_new).")]
    [TestCase("something_new", "  ", "GitHub sign-in failed (something_new).")]
    public void ParsesAnyOtherErrorAsFailedWithADescribingMessage(string error, string? description, string message)
    {
        var json = description is null
            ? $$"""{ "error": "{{error}}" }"""
            : $$"""{ "error": "{{error}}", "error_description": "{{description}}" }""";

        var result = DeviceFlowResponses.ParseTokenPoll(json);

        Assert.That(result.Status, Is.EqualTo(DeviceTokenStatus.Failed));
        Assert.That(result.Message, Is.EqualTo(message));
    }

    [TestCase("{}")]
    [TestCase("not json")]
    [TestCase("")]
    [TestCase("<html>Bad gateway</html>")]
    [TestCase("[]")]
    public void AnUnreadablePollOrOneWithNeitherATokenNorAnErrorIsRejected(string json)
    {
        Assert.Throws<GitHubException>(() => DeviceFlowResponses.ParseTokenPoll(json));
    }
}
