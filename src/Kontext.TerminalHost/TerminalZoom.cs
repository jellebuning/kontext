namespace Kontext.TerminalHost;

public sealed class TerminalZoom
{
    public const int MinimumSize = 6;
    public const int MaximumSize = 40;
    public const int DefaultSize = 13;

    public int FontSize { get; private set; } = DefaultSize;

    public event EventHandler? Changed;

    public void Apply(ZoomStep step)
    {
        var size = step switch
        {
            ZoomStep.In => FontSize + 1,
            ZoomStep.Out => FontSize - 1,
            ZoomStep.Reset => DefaultSize,
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, null),
        };

        size = Math.Clamp(size, MinimumSize, MaximumSize);
        if (size == FontSize)
        {
            return;
        }

        FontSize = size;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
