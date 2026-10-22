using Kontext.TerminalHost;

namespace Kontext.Terminal;

internal sealed class TerminalZoomHook : IDisposable
{
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmMouseWheel = 0x020A;
    private const int MkControl = 0x0008;
    private const nuint SubclassId = 1;

    private readonly TerminalZoom _zoom;
    private readonly ZoomGestureReader _reader = new();
    private readonly WindowSubclass _subclass;

    public TerminalZoomHook(nint window, TerminalZoom zoom)
    {
        _zoom = zoom;
        _subclass = new WindowSubclass(window, SubclassId, Handle);
    }

    public void Dispose() => _subclass.Dispose();

    private bool Handle(uint message, nint wParam, nint lParam, out nint result)
    {
        result = 0;

        switch (message)
        {
            case WmMouseWheel when (((int)wParam & 0xFFFF) & MkControl) != 0:
                var delta = (short)(wParam >> 16);
                if (_reader.FromWheel(delta) is { } wheelStep)
                {
                    _zoom.Apply(wheelStep);
                }

                return true;

            case WmKeyDown or WmKeyUp when NativeInputState.IsControlDown && ZoomGestureReader.FromKey((int)wParam) is { } keyStep:
                if (message == WmKeyDown)
                {
                    _zoom.Apply(keyStep);
                }

                return true;

            default:
                return false;
        }
    }
}
