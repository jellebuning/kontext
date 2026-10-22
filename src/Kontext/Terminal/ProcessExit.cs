using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using EasyWindowsTerminalControl.Internals;
using Microsoft.Win32.SafeHandles;

namespace Kontext.Terminal;

internal static class ProcessExit
{
    public static int? WaitForExitCode(IProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);

        var handle = TryOpenHandle(process);
        process.WaitForExit();

        return handle is { IsInvalid: false, IsClosed: false } && GetExitCodeProcess(handle, out var exitCode)
            ? exitCode
            : null;
    }

    private static SafeProcessHandle? TryOpenHandle(IProcess process)
    {
        try
        {
            var inner = process.GetType()
                .GetProperty("Process", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(process) as Process;

            return inner?.SafeHandle;
        }
        catch (Exception ex) when (ex is InvalidOperationException or TargetException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(SafeProcessHandle process, out int exitCode);
#pragma warning restore SYSLIB1054
}
