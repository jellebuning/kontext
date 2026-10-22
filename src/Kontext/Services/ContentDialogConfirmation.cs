using Kontext.Kernel;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Services;

internal sealed class ContentDialogConfirmation(XamlRootProvider root) : IConfirmation
{
    public async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = root.Current,
            Title = title,
            Content = message,
            PrimaryButtonText = confirmText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
