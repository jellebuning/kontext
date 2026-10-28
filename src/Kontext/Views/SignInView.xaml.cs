using System.ComponentModel;
using Kontext.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace Kontext.Views;

public sealed partial class SignInView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(ShellViewModel),
        typeof(SignInView),
        new PropertyMetadata(null, OnViewModelChanged));

    public SignInView() => InitializeComponent();

    public ShellViewModel? ViewModel
    {
        get => (ShellViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject view, DependencyPropertyChangedEventArgs args)
    {
        var signIn = (SignInView)view;

        if (args.OldValue is ShellViewModel previous)
        {
            previous.PropertyChanged -= signIn.ViewModel_PropertyChanged;
        }

        if (args.NewValue is ShellViewModel current)
        {
            current.PropertyChanged += signIn.ViewModel_PropertyChanged;
        }

        signIn.Bindings?.Update();
    }

    private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.VerificationUrl) && ViewModel?.VerificationUrl is { } url && LinkPolicy.IsSafeToOpen(url))
        {
            await Launcher.LaunchUriAsync(url);
        }
    }
}