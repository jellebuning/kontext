using Kontext.TerminalHost;

namespace Kontext.Terminal;

internal sealed class TerminalMouseHook : IDisposable
{
    private const uint WmMouseMove = 0x0200;
    private const uint WmLButtonDown = 0x0201;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmLButtonDoubleClick = 0x0203;
    private const uint WmRButtonDown = 0x0204;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmRButtonDoubleClick = 0x0206;
    private const uint WmMButtonDown = 0x0207;
    private const uint WmMButtonUp = 0x0208;
    private const uint WmMButtonDoubleClick = 0x0209;
    private const uint WmCaptureChanged = 0x0215;

    private const int MkShift = 0x0004;
    private const int MkControl = 0x0008;
    private const nuint SubclassId = 2;

    private readonly nint _window;
    private readonly MouseReportingState _state;
    private readonly Func<(int Columns, int Rows)> _grid;
    private readonly Action<string> _send;
    private readonly WindowSubclass _subclass;

    private readonly HashSet<MouseButton> _forwarded = [];

    private (int Column, int Row)? _lastMoveCell;

    public TerminalMouseHook(nint window, MouseReportingState state, Func<(int Columns, int Rows)> grid, Action<string> send)
    {
        _window = window;
        _state = state;
        _grid = grid;
        _send = send;
        _subclass = new WindowSubclass(window, SubclassId, Handle);
    }

    public void Dispose() => _subclass.Dispose();

    private bool Handle(uint message, nint wParam, nint lParam, out nint result)
    {
        result = 0;

        return message switch
        {
            WmLButtonDown or WmLButtonDoubleClick => Press(MouseButton.Left, wParam, lParam),
            WmMButtonDown or WmMButtonDoubleClick => Press(MouseButton.Middle, wParam, lParam),
            WmRButtonDown or WmRButtonDoubleClick => Press(MouseButton.Right, wParam, lParam),
            WmLButtonUp => Release(MouseButton.Left, wParam, lParam),
            WmMButtonUp => Release(MouseButton.Middle, wParam, lParam),
            WmRButtonUp => Release(MouseButton.Right, wParam, lParam),
            WmMouseMove => Move(wParam, lParam),
            WmCaptureChanged => LoseCapture(),
            _ => false,
        };
    }

    private bool Press(MouseButton button, nint wParam, nint lParam)
    {
        if (_state.Tracking == MouseTracking.Off || IsShiftDown(wParam))
        {
            return false;
        }

        if (_forwarded.Count == 0)
        {
            NativeInputState.CaptureMouse(_window);
        }

        _forwarded.Add(button);
        _lastMoveCell = null;
        Report(button, MouseAction.Press, wParam, lParam);
        return true;
    }

    private bool Release(MouseButton button, nint wParam, nint lParam)
    {
        if (!_forwarded.Remove(button))
        {
            return false;
        }

        _lastMoveCell = null;
        Report(button, MouseAction.Release, wParam, lParam);

        if (_forwarded.Count == 0)
        {
            NativeInputState.ReleaseMouse();
        }

        return true;
    }

    private bool LoseCapture()
    {
        _forwarded.Clear();
        _lastMoveCell = null;
        return false;
    }

    private bool Move(nint wParam, nint lParam)
    {
        var tracking = _state.Tracking;
        var held = _forwarded.Count > 0 ? _forwarded.Min() : (MouseButton?)null;

        var wanted = tracking switch
        {
            MouseTracking.AnyMotion => true,
            MouseTracking.Drag => held is not null,
            _ => false,
        };

        if (!wanted || (held is null && IsShiftDown(wParam)))
        {
            return false;
        }

        var cell = CellAt(lParam);
        if (cell == _lastMoveCell)
        {
            return true;
        }

        _lastMoveCell = cell;
        Report(held ?? MouseButton.None, MouseAction.Move, wParam, lParam);
        return true;
    }

    private void Report(MouseButton button, MouseAction action, nint wParam, nint lParam)
    {
        if (CellAt(lParam) is not { } cell)
        {
            return;
        }

        var modifiers = MouseModifiers.None;
        if (IsShiftDown(wParam))
        {
            modifiers |= MouseModifiers.Shift;
        }

        if (((int)wParam & MkControl) != 0)
        {
            modifiers |= MouseModifiers.Control;
        }

        if (NativeInputState.IsAltDown)
        {
            modifiers |= MouseModifiers.Alt;
        }

        if (MouseReportEncoder.Encode(button, action, cell.Column, cell.Row, modifiers, _state.UsesSgrEncoding) is { } report)
        {
            _send(report);
        }
    }

    private (int Column, int Row)? CellAt(nint lParam)
    {
        var (columns, rows) = _grid();
        var (width, height) = NativeInputState.ClientSizeOf(_window);
        if (columns <= 0 || rows <= 0 || width <= 0 || height <= 0)
        {
            return null;
        }

        var x = (short)(lParam & 0xFFFF);
        var y = (short)((lParam >> 16) & 0xFFFF);

        return (Math.Clamp((x * columns / width) + 1, 1, columns), Math.Clamp((y * rows / height) + 1, 1, rows));
    }

    private static bool IsShiftDown(nint wParam) => ((int)wParam & MkShift) != 0;
}
