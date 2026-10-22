using Kontext.Presentation;
using Kontext.Terminal;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Views;

public sealed partial class WorkspaceView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(ShellViewModel),
        typeof(WorkspaceView),
        new PropertyMetadata(null, (view, _) => ((WorkspaceView)view).Bindings?.Update()));

    public static readonly DependencyProperty ControlHostProperty = DependencyProperty.Register(
        nameof(ControlHost),
        typeof(TerminalControlHost),
        typeof(WorkspaceView),
        new PropertyMetadata(null, (view, _) => ((WorkspaceView)view).Bindings?.Update()));

    public WorkspaceView() => InitializeComponent();

    public ShellViewModel? ViewModel
    {
        get => (ShellViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public TerminalControlHost? ControlHost
    {
        get => (TerminalControlHost?)GetValue(ControlHostProperty);
        set => SetValue(ControlHostProperty, value);
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is IssueItemViewModel issue && ViewModel is not null)
        {
            ViewModel.Workspace.Sidebar.SelectedItem = issue;
        }
    }
}
