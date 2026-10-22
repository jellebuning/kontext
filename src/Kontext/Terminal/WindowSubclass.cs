using System.Runtime.InteropServices;

namespace Kontext.Terminal;

internal sealed class WindowSubclass : IDisposable
{
    private readonly nint _window;
    private readonly nuint _id;
    private readonly SubclassProc _procedure;

    public WindowSubclass(nint window, nuint id, MessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _window = window;
        _id = id;

        _procedure = (target, message, wParam, lParam, _, _) =>
            handler(message, wParam, lParam, out var result) ? result : DefSubclassProc(target, message, wParam, lParam);

        SetWindowSubclass(window, _procedure, id, 0);
    }

    public delegate bool MessageHandler(uint message, nint wParam, nint lParam, out nint result);

    private delegate nint SubclassProc(nint window, uint message, nint wParam, nint lParam, nuint subclassId, nuint referenceData);

    public void Dispose() => RemoveWindowSubclass(_window, _procedure, _id);

#pragma warning disable SYSLIB1054
    [DllImport("comctl32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint window, SubclassProc procedure, nuint subclassId, nuint referenceData);

    [DllImport("comctl32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint window, SubclassProc procedure, nuint subclassId);

    [DllImport("comctl32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint DefSubclassProc(nint window, uint message, nint wParam, nint lParam);
#pragma warning restore SYSLIB1054
}
