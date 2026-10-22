using System.Runtime.InteropServices;

namespace Kontext.Terminal;

internal static class NativeInputState
{
    private const int VkMenu = 0x12;
    private const int VkControl = 0x11;
    private const int VkShift = 0x10;

    public static bool IsAltDown => IsDown(VkMenu);

    public static bool IsControlDown => IsDown(VkControl);

    public static bool IsShiftDown => IsDown(VkShift);

    public static (int Width, int Height) ClientSizeOf(nint window)
        => GetClientRect(window, out var rect) ? (rect.Right - rect.Left, rect.Bottom - rect.Top) : (0, 0);

    public static void CaptureMouse(nint window) => SetCapture(window);

    public static void ReleaseMouse() => ReleaseCapture();

    private static bool IsDown(int virtualKey) => GetKeyState(virtualKey) < 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

#pragma warning disable SYSLIB1054
    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern short GetKeyState(int virtualKey);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint SetCapture(nint window);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint window, out Rect rect);
#pragma warning restore SYSLIB1054
}
