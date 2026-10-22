using Kontext.Domain;
using Kontext.Presentation;
using Kontext.Terminal;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Views;

public sealed partial class IssueDetailView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(IssueDetailViewModel),
        typeof(IssueDetailView),
        new PropertyMetadata(null, (view, _) => ((IssueDetailView)view).Bindings?.Update()));

    public static readonly DependencyProperty ControlHostProperty = DependencyProperty.Register(
        nameof(ControlHost),
        typeof(TerminalControlHost),
        typeof(IssueDetailView),
        new PropertyMetadata(null, (view, _) => ((IssueDetailView)view).Bindings?.Update()));

    public IssueDetailView() => InitializeComponent();

    public IssueDetailViewModel? ViewModel
    {
        get => (IssueDetailViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public TerminalControlHost? ControlHost
    {
        get => (TerminalControlHost?)GetValue(ControlHostProperty);
        set => SetValue(ControlHostProperty, value);
    }

    private void OpenInExplorer_Click(object sender, RoutedEventArgs e) => OpenIn(WorkspaceTool.Explorer);

    private void OpenInTerminal_Click(object sender, RoutedEventArgs e) => OpenIn(WorkspaceTool.Terminal);

    private void OpenInVisualStudio_Click(object sender, RoutedEventArgs e) => OpenIn(WorkspaceTool.VisualStudio);

    private void OpenIn(WorkspaceTool tool) => ViewModel?.OpenInCommand.Execute(tool);

    private void Tabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (ViewModel is not null)
        {
            ViewModel.ShowIssueTab = ReferenceEquals(sender.SelectedItem, IssueTab);
        }
    }
}
