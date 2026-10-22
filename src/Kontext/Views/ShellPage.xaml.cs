using Kontext.Presentation;
using Kontext.Terminal;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Views;

public sealed partial class ShellPage : Page
{
    public ShellPage(ShellViewModel viewModel, TerminalControlHost controlHost)
    {
        ViewModel = viewModel;
        ControlHost = controlHost;
        InitializeComponent();

        Loaded += (_, _) => ViewModel.InitializeCommand.Execute(null);
    }

    public ShellViewModel ViewModel { get; }

    public TerminalControlHost ControlHost { get; }
}
