using System.Collections.Concurrent;
using EasyWindowsTerminalControl;
using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;
using Kontext.TerminalHost;
using Microsoft.Terminal.Wpf;

namespace Kontext.Terminal;

internal sealed class EasyTerminalSessionService(TerminalControlHost host, TerminalZoom zoom, IUiDispatcher ui) : ITerminalSessionService
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private readonly ConcurrentDictionary<TerminalSessionId, EasyTerminalSession> _sessions = new();

    public async Task<ITerminalSession> StartAsync(TerminalSessionStart start, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(start);

        if (_sessions.TryRemove(start.Id, out var previous))
        {
            await ui.RunAsync(() => Discard(previous));
        }

        var commandLine = CommandLineBuilder.Build(start.Options.Command, start.Options.Arguments);
        var control = await ui.RunAsync(() => CreateAndAttach(commandLine, start.Options.WorkingDirectory));

        TermPTY pty;
        try
        {
            pty = await WaitUntilStartedAsync(control, commandLine, cancellationToken);
        }
        catch
        {
            await ui.RunAsync(() => Abandon(control));
            throw;
        }

        var exited = Task.Run(() => WaitForExit(pty), CancellationToken.None);
        var session = new EasyTerminalSession(start.Id, control, pty, exited);
        _sessions[start.Id] = session;
        return session;
    }

    public ITerminalSession? Find(TerminalSessionId id) => _sessions.GetValueOrDefault(id);

    public void Close(TerminalSessionId id)
    {
        if (_sessions.TryRemove(id, out var session))
        {
            _ = ui.RunAsync(() => Discard(session));
        }
    }

    public void Dispose()
    {
        foreach (var session in _sessions.Values)
        {
            Release(session.Pty);
        }

        _sessions.Clear();
    }

    private KontextTerminalControl CreateAndAttach(string commandLine, string workingDirectory)
    {
        var control = new KontextTerminalControl(zoom)
        {
            StartupCommandLine = commandLine,
            WorkingDirectory = workingDirectory,
            Win32InputMode = true,
            InputCapture = EasyTerminalControl.INPUT_CAPTURE.TabKey | EasyTerminalControl.INPUT_CAPTURE.DirectionKeys,
        };

        host.Add(control);
        return control;
    }

    private async Task<TermPTY> WaitUntilStartedAsync(EasyTerminalControl control, string commandLine, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(StartTimeout);

        try
        {
            while (true)
            {
                var pty = await ui.RunAsync(() => control.ConPTYTerm is { TermProcIsStarted: true } started ? started : null);
                if (pty is not null)
                {
                    return pty;
                }

                await Task.Delay(PollInterval, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TerminalStartException($"The terminal did not start in time. Command: {commandLine}");
        }
    }

    private static int? WaitForExit(TermPTY pty)
    {
        var process = pty.Process;
        return process is null ? null : ProcessExit.WaitForExitCode(process);
    }

    private void Abandon(EasyTerminalControl control)
    {
        if (control.ConPTYTerm is { } pty)
        {
            Release(pty);
        }

        host.Remove(control);
    }

    private void Discard(EasyTerminalSession session)
    {
        Release(session.Pty);
        host.Remove(session.Control);
    }

    private static void Release(TermPTY pty)
    {
        var process = pty.Process;
        if (process is { HasExited: false })
        {
            process.Kill(EntireProcessTree: true);
        }

        try
        {
            (pty as ITerminalConnection)?.Close();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
        }
    }
}
