using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Terminal;

public sealed class AutomationLeafGrid : Grid
{
    protected override AutomationPeer OnCreateAutomationPeer() => new AutomationLeafPeer(this);
}
