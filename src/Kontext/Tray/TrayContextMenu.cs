using Kontext.Services;

namespace Kontext.Tray;

internal sealed class TrayContextMenu(IWindowHandleProvider window, MenuTheme theme)
{
    public TrayCommand? Choose()
    {
        theme.FollowSystem();

        var menu = NativeMethods.CreatePopupMenu();
        try
        {
            NativeMethods.AppendMenu(menu, NativeMethods.MfString, (nuint)TrayCommand.Open, "Open Kontext");
            NativeMethods.AppendMenu(menu, NativeMethods.MfSeparator, 0, null);
            NativeMethods.AppendMenu(menu, NativeMethods.MfString, (nuint)TrayCommand.Exit, "Exit");

            NativeMethods.GetCursorPos(out var cursor);

            NativeMethods.SetForegroundWindow(window.Handle);

            var chosen = NativeMethods.TrackPopupMenuEx(
                menu,
                NativeMethods.TpmRightButton | NativeMethods.TpmReturnCommand | NativeMethods.TpmBottomAlign,
                cursor.X,
                cursor.Y,
                window.Handle,
                IntPtr.Zero);

            NativeMethods.PostMessage(window.Handle, NativeMethods.WmNull, IntPtr.Zero, IntPtr.Zero);

            return chosen == 0 ? null : (TrayCommand)chosen;
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }
}
