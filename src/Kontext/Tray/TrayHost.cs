using Kontext.Presentation;
using Kontext.Services;
using Microsoft.UI.Windowing;

namespace Kontext.Tray;

internal sealed class TrayHost(MainWindow window, NotifyIcon icon, TrayContextMenu menu, AppLifetime lifetime)
{
    public void Start()
    {
        window.AppWindow.Closing += OnClosing;
        icon.Activated += (_, _) => Restore();
        icon.ContextMenuRequested += (_, _) => OnMenuRequested();
        icon.Show();
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!lifetime.ClosingHidesWindow)
        {
            return;
        }

        args.Cancel = true;
        sender.Hide();
    }

    private void OnMenuRequested()
    {
        switch (menu.Choose())
        {
            case TrayCommand.Open:
                Restore();
                break;
            case TrayCommand.Exit:
                lifetime.RequestExit();
                window.Close();
                break;
        }
    }

    private void Restore()
    {
        if (window.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
        }

        window.AppWindow.Show();
        window.Activate();
    }
}
