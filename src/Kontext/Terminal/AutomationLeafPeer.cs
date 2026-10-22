using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;

namespace Kontext.Terminal;

internal sealed class AutomationLeafPeer(FrameworkElement owner) : FrameworkElementAutomationPeer(owner)
{
    protected override string GetClassNameCore() => Owner.GetType().Name;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

    protected override IList<AutomationPeer>? GetChildrenCore() => null;
}
