using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.TestSupport;

public sealed class FakeGitHubDeviceLogin : IGitHubDeviceLogin
{
    private TaskCompletionSource<string>? _pendingToken;

    public DeviceCodeChallenge Challenge { get; set; } = new(
        "device-code",
        "WDJB-MJHT",
        new Uri("https://github.com/login/device"),
        DateTimeOffset.Parse("2025-03-01T10:15:00Z", System.Globalization.CultureInfo.InvariantCulture),
        TimeSpan.FromSeconds(5));

    public string Token { get; set; } = "gho_fake";

    public Exception? StartFailsWith { get; set; }

    public Exception? WaitFailsWith { get; set; }

    public bool Deferred { get; set; }

    public int Started { get; private set; }

    public int Waited { get; private set; }

    public void Grant(string? token = null) => _pendingToken!.SetResult(token ?? Token);

    public Task<DeviceCodeChallenge> StartAsync(CancellationToken cancellationToken = default)
    {
        Started++;
        return StartFailsWith is null ? Task.FromResult(Challenge) : Task.FromException<DeviceCodeChallenge>(StartFailsWith);
    }

    public Task<string> WaitForAccessTokenAsync(DeviceCodeChallenge challenge, CancellationToken cancellationToken = default)
    {
        Waited++;

        if (WaitFailsWith is not null)
        {
            return Task.FromException<string>(WaitFailsWith);
        }

        if (!Deferred)
        {
            return Task.FromResult(Token);
        }

        _pendingToken = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(() => _pendingToken.TrySetCanceled(cancellationToken));
        return _pendingToken.Task;
    }
}
