using System.Net;
using System.Text;

namespace Kontext.GitHub.Tests;

internal sealed record RecordedRequest(HttpMethod Method, string Url, string Accept, Dictionary<string, string> Form);

internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();
    private readonly List<RecordedRequest> _requests = [];
    private readonly object _gate = new();

    public IReadOnlyList<RecordedRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    public void Enqueue(HttpStatusCode status, string body) =>
        _responses.Enqueue(() => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });

    public void EnqueueFailure(Exception exception) => _responses.Enqueue(() => throw exception);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var content = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var form = content
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(parts => WebUtility.UrlDecode(parts[0]), parts => WebUtility.UrlDecode(parts.Length > 1 ? parts[1] : string.Empty));

        lock (_gate)
        {
            _requests.Add(new RecordedRequest(request.Method, request.RequestUri!.ToString(), request.Headers.Accept.ToString(), form));
        }

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No response was queued for this request.");
        }

        return _responses.Dequeue()();
    }
}
