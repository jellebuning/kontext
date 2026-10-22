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
    public void ParsesTheDeviceCodeResponseIntoAChallenge()
    {
        var challenge = DeviceFlowResponses.ParseDeviceCode(DeviceCodeJson, Now);

        Assert.That(challenge.DeviceCode, Is.EqualTo("3584d83530557fdd1f46af8289938c8ef79f9dc5"));
        Assert.That(challenge.UserCode, Is.EqualTo("WDJB-MJHT"));
        Assert.That(challenge.VerificationUri, Is.EqualTo(new Uri("https://github.com/login/device")));
        Assert.That(challenge.PollInterval, Is.EqualTo(TimeSpan.FromSeconds(7)));
    }

    [Test]
    public void ComputesTheExpiryFromTheTimePassedIn()
    {
        var challenge = DeviceFlowResponses.ParseDeviceCode(DeviceCodeJson, Now);

        Assert.That(challenge.ExpiresAt, Is.EqualTo(Now.AddSeconds(900)));
    }

    [Test]
    public void FallsBackToFiveSecondsWhenTheDeviceCodeResponseHasNoInterval()
    {
        var json = DeviceCodeJson.Replace(",\n  \"interval\": 7", string.Empty, StringComparison.Ordinal).Replace(",\r\n  \"interval\": 7", string.Empty, StringComparison.Ordinal);

        var challenge = DeviceFlowResponses.ParseDeviceCode(json, Now);

        Assert.That(challenge.PollInterval, Is.EqualTo(TimeSpan.FromSeconds(5)));
    }

    [TestCase("0")]
    [TestCase("-3")]
    public void FallsBackToFiveSecondsWhenTheIntervalIsNotPositive(string interval)
    {
        var json = DeviceCodeJson.Replace("\"interval\": 7", $"\"interval\": {interval}", StringComparison.Ordinal);

        var challenge = DeviceFlowResponses.ParseDeviceCode(json, Now);

        Assert.That(challenge.PollInterval, Is.EqualTo(TimeSpan.FromSeconds(5)));
    }

    [Test]
    public void ADeviceCodeResponseCarryingAnErrorThrowsWithTheDescription()
    {
        const string json = """{ "error": "device_flow_disabled", "error_description": "Device Flow must be explicitly enabled for this App" }""";

        var ex = Assert.Throws<GitHubException>(() => DeviceFlowResponses.ParseDeviceCode(json, Now));

        Assert.That(ex.Message, Is.EqualTo("GitHub sign-in failed: Device Flow must be explicitly enabled for this App"));
    }

    [Test]
    public void ADeviceCodeResponseCarryingAnErrorWithoutDescriptionNamesTheError()
    {
        const string json = """{ "error": "incorrect_client_credentials" }""";

        var ex = Assert.Throws<GitHubException>(() => DeviceFlowResponses.ParseDeviceCode(json, Now));

        Assert.That(ex.Message, Is.EqualTo("GitHub sign-in failed (incorrect_client_credentials)."));
    }

    [TestCase("""{ "user_code": "A", "verification_uri": "https://github.com/login/device", "expires_in": 900 }""", TestName = "NoDeviceCode")]
    [TestCase("""{ "device_code": "d", "verification_uri": "https://github.com/login/device", "expires_in": 900 }""", TestName = "NoUserCode")]
    [TestCase("""{ "device_code": "d", "user_code": "A", "expires_in": 900 }""", TestName = "NoVerificationUri")]
    [TestCase("""{ "device_code": "d", "user_code": "A", "verification_uri": "not a uri", "expires_in": 900 }""", TestName = "VerificationUriNotAbsolute")]
    [TestCase("""{ "device_code": "d", "user_code": "A", "verification_uri": "https://github.com/login/device" }""", TestName = "NoExpiry")]
    [TestCase("""{ "device_code": "", "user_code": "A", "verification_uri": "https://github.com/login/device", "expires_in": 900 }""", TestName = "BlankDeviceCode")]
    public void ADeviceCodeResponseMissingAFieldIsRejected(string json)
    {
        Assert.Throws<GitHubException>(() => DeviceFlowResponses.ParseDeviceCode(json, Now));
    }

    [TestCase("not json")]
    [TestCase("")]
    [TestCase("[1, 2]")]
    [TestCase("\"text\"")]
    public void MalformedDeviceCodeJsonThrowsAGitHubException(string json)
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

    [Test]
    public void ParsesAPendingPoll()
    {
        var result = DeviceFlowResponses.ParseTokenPoll("""{ "error": "authorization_pending", "error_description": "The authorization request is still pending." }""");

        Assert.That(result.Status, Is.EqualTo(DeviceTokenStatus.Pending));
        Assert.That(result.Token, Is.Null);
    }

    [Test]
    public void ParsesASlowDownPollWithTheNewInterval()
    {
        var result = DeviceFlowResponses.ParseTokenPoll("""{ "error": "slow_down", "interval": 10 }""");

        Assert.That(result.Status, Is.EqualTo(DeviceTokenStatus.SlowDown));
        Assert.That(result.IntervalSeconds, Is.EqualTo(10));
    }

    [Test]
    public void ParsesASlowDownPollWithoutAnInterval()
    {
        var result = DeviceFlowResponses.ParseTokenPoll("""{ "error": "slow_down" }""");

        Assert.That(result.Status, Is.EqualTo(DeviceTokenStatus.SlowDown));
        Assert.That(result.IntervalSeconds, Is.Null);
    }

    [Test]
    public void ParsesAnExpiredPoll()
    {
        var result = DeviceFlowResponses.ParseTokenPoll("""{ "error": "expired_token" }""");

        Assert.That(result.Status, Is.EqualTo(DeviceTokenStatus.Expired));
    }

    [Test]
    public void ParsesADeniedPoll()
    {
        var result = DeviceFlowResponses.ParseTokenPoll("""{ "error": "access_denied" }""");

        Assert.That(result.Status, Is.EqualTo(DeviceTokenStatus.Denied));
    }

    [TestCase("device_flow_disabled", "Device Flow must be explicitly enabled for this App")]
    [TestCase("incorrect_client_credentials", "The client_id and/or client_secret passed are incorrect.")]
    [TestCase("something_new", null)]
    public void ParsesAnyOtherErrorAsFailedCarryingTheErrorAndDescription(string error, string? description)
    {
        var json = description is null
            ? $$"""{ "error": "{{error}}" }"""
            : $$"""{ "error": "{{error}}", "error_description": "{{description}}" }""";

        var result = DeviceFlowResponses.ParseTokenPoll(json);

        Assert.That(result.Status, Is.EqualTo(DeviceTokenStatus.Failed));
        Assert.That(result.Error, Is.EqualTo(error));
        Assert.That(result.Description, Is.EqualTo(description));
    }

    [Test]
    public void APollWithNeitherATokenNorAnErrorIsRejected()
    {
        Assert.Throws<GitHubException>(() => DeviceFlowResponses.ParseTokenPoll("{}"));
    }

    [TestCase("not json")]
    [TestCase("")]
    [TestCase("<html>Bad gateway</html>")]
    [TestCase("[]")]
    public void MalformedPollJsonThrowsAGitHubException(string json)
    {
        Assert.Throws<GitHubException>(() => DeviceFlowResponses.ParseTokenPoll(json));
    }

    [Test]
    public void DescribeUsesTheDescriptionWhenThereIsOne()
    {
        Assert.That(DeviceFlowResponses.Describe("device_flow_disabled", "Device Flow must be enabled"), Is.EqualTo("GitHub sign-in failed: Device Flow must be enabled"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void DescribeNamesTheErrorWhenThereIsNoDescription(string? description)
    {
        Assert.That(DeviceFlowResponses.Describe("device_flow_disabled", description), Is.EqualTo("GitHub sign-in failed (device_flow_disabled)."));
    }
}
