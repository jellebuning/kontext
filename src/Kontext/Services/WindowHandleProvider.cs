namespace Kontext.Services;

internal sealed class WindowHandleProvider : IWindowHandleProvider
{
    public nint Handle { get; private set; }

    public void Attach(nint handle) => Handle = handle;
}
