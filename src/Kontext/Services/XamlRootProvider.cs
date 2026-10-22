using Microsoft.UI.Xaml;

namespace Kontext.Services;

public sealed class XamlRootProvider
{
    private FrameworkElement? _root;

    public XamlRoot? Current => _root?.XamlRoot;

    public void Attach(FrameworkElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _root = root;
    }
}
