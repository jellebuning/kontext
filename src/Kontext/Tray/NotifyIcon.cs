using System.Runtime.InteropServices;
using Kontext.Services;

namespace Kontext.Tray;

internal sealed class NotifyIcon(IWindowHandleProvider window, AppIconOptions options) : IDisposable
{
    private const uint IconId = 1;
    private const uint CallbackMessage = NativeMethods.WmApp + 1;
    private const nuint SubclassId = 1;
    private const string Tooltip = "Kontext";

    private NativeMethods.SubclassProc? _procedure;
    private nint _icon;
    private uint _taskbarCreated;
    private bool _shown;

    public event EventHandler? Activated;

    public event EventHandler? ContextMenuRequested;

    public void Show()
    {
        if (_shown)
        {
            return;
        }

        _taskbarCreated = NativeMethods.RegisterWindowMessage("TaskbarCreated");

        var width = NativeMethods.GetSystemMetrics(NativeMethods.SmCxSmallIcon);
        var height = NativeMethods.GetSystemMetrics(NativeMethods.SmCySmallIcon);
        _icon = NativeMethods.LoadImage(IntPtr.Zero, options.IconPath, NativeMethods.ImageIcon, width, height, NativeMethods.LoadFromFile);

        _procedure = OnWindowMessage;
        NativeMethods.SetWindowSubclass(window.Handle, _procedure, SubclassId, 0);

        Add();
        _shown = true;
    }

    public void Dispose()
    {
        if (!_shown)
        {
            return;
        }

        var data = CreateData();
        NativeMethods.ShellNotifyIcon(NativeMethods.NimDelete, ref data);
        NativeMethods.RemoveWindowSubclass(window.Handle, _procedure!, SubclassId);
        if (_icon != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_icon);
        }

        _shown = false;
    }

    private void Add()
    {
        var data = CreateData();
        data.Flags = NativeMethods.NifMessage | NativeMethods.NifIcon | NativeMethods.NifTip;
        data.CallbackMessage = CallbackMessage;
        data.Icon = _icon;
        data.Tip = Tooltip;
        NativeMethods.ShellNotifyIcon(NativeMethods.NimAdd, ref data);
    }

    private NativeMethods.NotifyIconData CreateData() => new()
    {
        Size = Marshal.SizeOf<NativeMethods.NotifyIconData>(),
        Window = window.Handle,
        Id = IconId,
        Tip = string.Empty,
        Info = string.Empty,
        InfoTitle = string.Empty,
    };

    private nint OnWindowMessage(nint handle, uint message, nint wParam, nint lParam, nuint subclassId, nuint referenceData)
    {
        if (message == CallbackMessage)
        {
            switch ((uint)lParam)
            {
                case NativeMethods.WmLeftButtonUp:
                    Activated?.Invoke(this, EventArgs.Empty);
                    break;
                case NativeMethods.WmRightButtonUp:
                    ContextMenuRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }

            return 0;
        }

        if (_taskbarCreated != 0 && message == _taskbarCreated)
        {
            Add();
        }

        return NativeMethods.DefSubclassProc(handle, message, wParam, lParam);
    }
}
