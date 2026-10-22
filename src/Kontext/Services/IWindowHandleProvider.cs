namespace Kontext.Services;

public interface IWindowHandleProvider
{
    nint Handle { get; }

    void Attach(nint handle);
}
