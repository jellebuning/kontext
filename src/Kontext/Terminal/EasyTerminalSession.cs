using EasyWindowsTerminalControl;
using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.Terminal;

internal sealed class EasyTerminalSession(TerminalSessionId id, EasyTerminalControl control, TermPTY pty, Task<int?> exited) : ITerminalSession
{
    public TerminalSessionId Id { get; } = id;

    public EasyTerminalControl Control { get; } = control;

    public TermPTY Pty { get; } = pty;

    public Task<int?> Exited { get; } = exited;

    public bool HasExited => Exited.IsCompleted;

    public void Write(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Pty.WriteToTerm(text);
    }
}
