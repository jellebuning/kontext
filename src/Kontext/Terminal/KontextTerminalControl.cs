using System.Reflection;
using EasyWindowsTerminalControl;
using Kontext.TerminalHost;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Kontext.Terminal;

internal sealed class KontextTerminalControl : EasyTerminalControl, IDisposable
{
    private static readonly MethodInfo StartWhenLoaded =
        typeof(EasyTerminalControl).GetMethod("Terminal_Loaded", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("EasyWindowsTerminalControl no longer has the expected Terminal_Loaded handler; update KontextTerminalControl.");

    private readonly TerminalZoom _zoom;
    private readonly MouseReportingState _mouse = new();
    private TerminalZoomHook? _zoomHook;
    private TerminalMouseHook? _mouseHook;

    public KontextTerminalControl(TerminalZoom zoom)
    {
        _zoom = zoom;

        var terminal = new SafeTerminalControl { AutoResize = true };

        VendorTerminalParts.ScrollBarOf(terminal).IndicatorMode = ScrollingIndicatorMode.MouseIndicator;

        terminal.Loaded += (_, _) =>
        {
            StartWhenLoaded.Invoke(this, [null, null]);
            _zoom.Changed += ApplyZoom;
        };
        terminal.Unloaded += (_, _) => Dispose();

        terminal.SizeChanged += (_, _) => AttachWhenTheWindowExists(terminal);

        Terminal = terminal;
        Content = new Grid { Children = { terminal } };

        FontFamilyWhenSettingTheme = TerminalAppearance.Font;
        FontSizeWhenSettingTheme = zoom.FontSize;
        Theme = TerminalAppearance.Theme;

        ConPTYTerm.InterceptOutputToUITerminal = ObserveOutput;
    }

    public void Dispose()
    {
        _zoom.Changed -= ApplyZoom;
        _zoomHook?.Dispose();
        _zoomHook = null;
        _mouseHook?.Dispose();
        _mouseHook = null;
    }

    private void ObserveOutput(ref Span<char> output) => _mouse.Observe(output);

    private void AttachWhenTheWindowExists(SafeTerminalControl terminal)
    {
        if (_zoomHook is not null)
        {
            return;
        }

        var window = VendorTerminalParts.WindowOf(terminal);
        if (window == 0)
        {
            return;
        }

        _zoomHook = new TerminalZoomHook(window, _zoom);
        _mouseHook = new TerminalMouseHook(window, _mouse, () => (terminal.Columns, terminal.Rows), SendToProgram);
        ApplyZoom(this, EventArgs.Empty);
    }

    private void SendToProgram(string text)
    {
        try
        {
            ConPTYTerm?.WriteToTerm(text);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
    }

    private void ApplyZoom(object? sender, EventArgs e)
    {
        FontSizeWhenSettingTheme = _zoom.FontSize;
        Theme = TerminalAppearance.Theme;
    }
}
