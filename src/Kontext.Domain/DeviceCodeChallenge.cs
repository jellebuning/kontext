namespace Kontext.Domain;

public sealed record DeviceCodeChallenge(string DeviceCode, string UserCode, Uri VerificationUri, DateTimeOffset ExpiresAt, TimeSpan PollInterval);
