using Kontext.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Terminal;

public sealed partial class TerminalSurface : UserControl
{
    public static readonly DependencyProperty ControlHostProperty = DependencyProperty.Register(
        nameof(ControlHost),
        typeof(TerminalControlHost),
        typeof(TerminalSurface),
        new PropertyMetadata(null, (surface, _) => ((TerminalSurface)surface).Attach()));

    public static readonly DependencyProperty SessionProperty = DependencyProperty.Register(
        nameof(Session),
        typeof(ITerminalSession),
        typeof(TerminalSurface),
        new PropertyMetadata(null, (surface, _) => ((TerminalSurface)surface).ShowSession()));

    public TerminalSurface() => InitializeComponent();

    public TerminalControlHost? ControlHost
    {
        get => (TerminalControlHost?)GetValue(ControlHostProperty);
        set => SetValue(ControlHostProperty, value);
    }

    public ITerminalSession? Session
    {
        get => (ITerminalSession?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    private void Attach()
    {
        ControlHost?.Attach(Root);
        ShowSession();
    }

    private void ShowSession() => ControlHost?.Show((Session as EasyTerminalSession)?.Control);
}
