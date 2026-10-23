using Kontext.Domain;

namespace Kontext.Contracts;

public interface IGitHubDeviceLogin
{
    Task<DeviceCodeChallenge> StartAsync(CancellationToken cancellationToken = default);

    Task<string> WaitForAccessTokenAsync(DeviceCodeChallenge challenge, CancellationToken cancellationToken = default);
}
