using Kontext.Domain;

namespace Kontext.Claude;

internal sealed class ClaudeHooks(IClaudeHookServer server, ClaudeActivityReporter reporter) : IClaudeHooks
{
    private readonly Lock _gate = new();

    private Uri? _address;
    private bool _started;

    public string? SettingsFor(TerminalSessionId session)
    {
        var address = EnsureStarted();
        return address is null ? null : ClaudeHookSettingsBuilder.Build(address, session);
    }

    private Uri? EnsureStarted()
    {
        lock (_gate)
        {
            if (!_started)
            {
                _started = true;
                _address = server.Start();

                if (_address is not null)
                {
                    server.EventReceived += (_, e) => reporter.OnHook(e.Hook);
                }
            }

            return _address;
        }
    }
}
