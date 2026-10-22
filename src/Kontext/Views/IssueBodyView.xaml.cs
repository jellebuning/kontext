using CommunityToolkit.WinUI.Controls;
using Kontext.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace Kontext.Views;

public sealed partial class IssueBodyView : UserControl
{
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown),
        typeof(string),
        typeof(IssueBodyView),
        new PropertyMetadata(string.Empty, (view, args) => ((IssueBodyView)view).Renderer.Text = (string)args.NewValue ?? string.Empty));

    public IssueBodyView() => InitializeComponent();

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private async void Renderer_LinkClicked(object? sender, LinkClickedEventArgs e)
    {
        e.Handled = true;

        if (LinkPolicy.IsSafeToOpen(e.Uri))
        {
            await Launcher.LaunchUriAsync(e.Uri);
        }
    }
}
