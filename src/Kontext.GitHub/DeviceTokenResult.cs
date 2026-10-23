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
    string? Message = null);
