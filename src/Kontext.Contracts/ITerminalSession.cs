using Kontext.Domain;

namespace Kontext.Contracts;

public interface ITerminalSession
{
    TerminalSessionId Id { get; }

    bool HasExited { get; }

    Task<int?> Exited { get; }

    void Write(string text);
}
