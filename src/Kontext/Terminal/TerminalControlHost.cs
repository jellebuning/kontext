using EasyWindowsTerminalControl;
using Kontext.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Terminal;

public sealed class TerminalControlHost
{
    private Panel? _panel;

    public void Attach(Panel panel) => _panel = panel;

    public void Add(EasyTerminalControl control)
    {
        ArgumentNullException.ThrowIfNull(control);

        var panel = _panel ?? throw new TerminalStartException("The terminal area is not ready yet.");
        control.Visibility = Visibility.Collapsed;
        panel.Children.Add(control);
    }

    public void Remove(EasyTerminalControl control)
    {
        control.Visibility = Visibility.Collapsed;
        _panel?.Children.Remove(control);
    }

    public void Show(EasyTerminalControl? control)
    {
        if (_panel is null)
        {
            return;
        }

        foreach (var child in _panel.Children.Where(child => !ReferenceEquals(child, control)))
        {
            child.Visibility = Visibility.Collapsed;
        }

        if (control is not null)
        {
            control.Visibility = Visibility.Visible;
            control.Focus(FocusState.Programmatic);
        }
    }
}