using Kontext.Domain;

namespace Kontext.Contracts;

public interface ITerminalSessionService : IDisposable
{
    Task<ITerminalSession> StartAsync(TerminalSessionStart start, CancellationToken cancellationToken = default);

    ITerminalSession? Find(TerminalSessionId id);

    void Close(TerminalSessionId id);
}
