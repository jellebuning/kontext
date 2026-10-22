namespace Kontext.TerminalHost;

public sealed class ZoomGestureReader
{
    private const int WheelNotch = 120;

    private const int VkZero = 0x30;
    private const int VkNumpadZero = 0x60;
    private const int VkNumpadAdd = 0x6B;
    private const int VkNumpadSubtract = 0x6D;
    private const int VkOemPlus = 0xBB;
    private const int VkOemMinus = 0xBD;

    private int _pendingWheel;

    public static ZoomStep? FromKey(int virtualKey) => virtualKey switch
    {
        VkOemPlus or VkNumpadAdd => ZoomStep.In,
        VkOemMinus or VkNumpadSubtract => ZoomStep.Out,
        VkZero or VkNumpadZero => ZoomStep.Reset,
        _ => null,
    };

    public ZoomStep? FromWheel(int delta)
    {
        _pendingWheel += delta;
        if (Math.Abs(_pendingWheel) < WheelNotch)
        {
            return null;
        }

        var step = _pendingWheel > 0 ? ZoomStep.In : ZoomStep.Out;
        _pendingWheel = 0;
        return step;
    }
}
