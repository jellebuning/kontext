using System.Globalization;
using System.Net;
using Kontext.Domain;

namespace Kontext.Claude;

internal sealed class LoopbackClaudeHookServer : IClaudeHookServer, IDisposable
{
    private const int FirstPort = 49152;
    private const int PortCount = 16000;
    private const int Attempts = 20;

    private readonly Lock _gate = new();

    private HttpListener? _listener;
    private Uri? _address;
    private bool _started;

    public event EventHandler<ClaudeHookEventArgs>? EventReceived;

    public Uri? Start()
    {
        lock (_gate)
        {
            if (_started)
            {
                return _address;
            }

            _started = true;

            for (var attempt = 0; attempt < Attempts; attempt++)
            {
                var port = FirstPort + Random.Shared.Next(PortCount);
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");

                try
                {
                    listener.Start();
                }
                catch (HttpListenerException)
                {
                    listener.Close();
                    continue;
                }

                _listener = listener;
                _address = new Uri($"http://127.0.0.1:{port}/");
                _ = Task.Run(() => ServeAsync(listener));
                return _address;
            }

            return null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _listener?.Close();
            _listener = null;
        }
    }

    private async Task ServeAsync(HttpListener listener)
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            await HandleAsync(context);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        string body;
        try
        {
            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
            body = await reader.ReadToEndAsync();
        }
        catch (Exception ex) when (ex is IOException or HttpListenerException or ObjectDisposedException)
        {
            body = string.Empty;
        }

        var session = SessionIn(context.Request.Url);
        Respond(context);

        if (session is { } id && ClaudeHookPayload.Parse(body) is var (name, notificationType))
        {
            EventReceived?.Invoke(this, new ClaudeHookEventArgs(new ClaudeHookEvent(id, name, notificationType)));
        }
    }

    private static void Respond(HttpListenerContext context)
    {
        try
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentLength64 = 0;
            context.Response.Close();
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
        {
        }
    }

    private static TerminalSessionId? SessionIn(Uri? url)
    {
        var segments = url?.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments is ["hook", var id] && Guid.TryParse(id, CultureInfo.InvariantCulture, out var guid) ? new TerminalSessionId(guid) : null;
    }
}
