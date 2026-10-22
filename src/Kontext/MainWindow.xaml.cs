using Kontext.Services;
using Kontext.Views;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Kontext;

public sealed partial class MainWindow : Window
{
    public MainWindow(ShellPage shellPage, IWindowHandleProvider windowHandle, AppIconOptions icon, XamlRootProvider xamlRoot)
    {
        InitializeComponent();

        AppWindow.SetIcon(icon.IconPath);

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        windowHandle.Attach(WindowNative.GetWindowHandle(this));
        xamlRoot.Attach(RootFrame);
        RootFrame.Content = shellPage;
    }
}
