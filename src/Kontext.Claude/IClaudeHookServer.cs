namespace Kontext.Claude;

internal interface IClaudeHookServer
{
    event EventHandler<ClaudeHookEventArgs>? EventReceived;

    Uri? Start();
}
