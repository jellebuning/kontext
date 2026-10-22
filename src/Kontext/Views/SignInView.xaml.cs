using Kontext.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Kontext.Views;

public sealed partial class SignInView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(ShellViewModel),
        typeof(SignInView),
        new PropertyMetadata(null, (view, _) => ((SignInView)view).Bindings?.Update()));

    public SignInView() => InitializeComponent();

    public ShellViewModel? ViewModel
    {
        get => (ShellViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void TokenBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel?.SignInCommand.CanExecute(null) == true)
        {
            ViewModel.SignInCommand.Execute(null);
        }
    }
}
