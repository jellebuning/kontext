using System.Reflection;
using Microsoft.Terminal.Wpf;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace Kontext.Terminal;

internal sealed class SafeTerminalControl : TerminalControl
{
    private static readonly MethodInfo VendorGettingFocus =
        typeof(TerminalControl).GetMethod("TerminalControl_GettingFocus", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("The terminal control no longer has the expected TerminalControl_GettingFocus handler; update SafeTerminalControl.");

    public SafeTerminalControl()
    {
        var vendor = (TypedEventHandler<UIElement, GettingFocusEventArgs>)Delegate.CreateDelegate(
            typeof(TypedEventHandler<UIElement, GettingFocusEventArgs>), this, VendorGettingFocus);

        GettingFocus -= vendor;
        GettingFocus += (sender, args) =>
        {
            if (args.TryCancel())
            {
                vendor(sender, args);
            }
        };
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AutomationLeafPeer(this);
}
