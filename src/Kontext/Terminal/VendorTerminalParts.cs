using System.Reflection;
using Microsoft.Terminal.Wpf;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Kontext.Terminal;

internal static class VendorTerminalParts
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly FieldInfo ScrollBarField = typeof(TerminalControl).GetField("scrollbar", Private)
        ?? throw new InvalidOperationException("The terminal control no longer has the expected scrollbar field; update VendorTerminalParts.");

    private static readonly FieldInfo ContainerField = typeof(TerminalControl).GetField("termContainer", Private)
        ?? throw new InvalidOperationException("The terminal control no longer has the expected termContainer field; update VendorTerminalParts.");

    private static readonly PropertyInfo WindowProperty = ContainerField.FieldType.GetProperty("Hwnd", Private)
        ?? throw new InvalidOperationException("The terminal container no longer has the expected Hwnd property; update VendorTerminalParts.");

    public static ScrollBar ScrollBarOf(TerminalControl terminal) => (ScrollBar)ScrollBarField.GetValue(terminal)!;

    public static nint WindowOf(TerminalControl terminal) => (nint)WindowProperty.GetValue(ContainerField.GetValue(terminal))!;
}
