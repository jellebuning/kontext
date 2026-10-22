using Kontext.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Views;

public sealed class SidebarTemplateSelector : DataTemplateSelector
{
    public DataTemplate? GroupTemplate { get; set; }

    public DataTemplate? IssueTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item is RepositoryGroupViewModel ? GroupTemplate : IssueTemplate;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
