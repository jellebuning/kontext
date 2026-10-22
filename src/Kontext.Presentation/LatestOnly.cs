namespace Kontext.Presentation;

internal sealed class LatestOnly : IDisposable
{
    private CancellationTokenSource? _current;

    public CancellationTokenSource Begin()
    {
        _current?.Cancel();
        _current = new CancellationTokenSource();
        return _current;
    }

    public bool IsCurrent(CancellationTokenSource operation) => ReferenceEquals(_current, operation);

    public void CancelCurrent()
    {
        _current?.Cancel();
        _current = null;
    }

    public void Dispose()
    {
        _current?.Cancel();
        _current?.Dispose();
        _current = null;
    }
}
