using Kontext.TerminalHost;
using Windows.ApplicationModel.DataTransfer;

namespace Kontext.Terminal;

internal sealed class TerminalKeyboardHook : IDisposable
{
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmChar = 0x0102;
    private const int VkC = 0x43;
    private const int VkV = 0x56;
    private const nint CharCopy = 0x03;
    private const nint CharPaste = 0x16;
    private const nuint SubclassId = 3;

    private readonly MouseReportingState _state;
    private readonly Func<string> _selectedText;
    private readonly Action<string> _send;
    private readonly WindowSubclass _subclass;

    private readonly HashSet<int> _handledKeys = [];

    private bool _swallowChar;

    public TerminalKeyboardHook(nint window, MouseReportingState state, Func<string> selectedText, Action<string> send)
    {
        _state = state;
        _selectedText = selectedText;
        _send = send;
        _subclass = new WindowSubclass(window, SubclassId, Handle);
    }

    public void Dispose() => _subclass.Dispose();

    private bool Handle(uint message, nint wParam, nint lParam, out nint result)
    {
        result = 0;

        switch (message)
        {
            case WmKeyDown:
                return KeyDown((int)wParam);

            case WmKeyUp:
                return _handledKeys.Remove((int)wParam);

            case WmChar:
                var swallow = _swallowChar && wParam is CharCopy or CharPaste;
                _swallowChar = false;
                return swallow;

            default:
                return false;
        }
    }

    private bool KeyDown(int virtualKey)
    {
        var shortcut = TerminalShortcutReader.FromKey(
            virtualKey, NativeInputState.IsControlDown, NativeInputState.IsShiftDown, NativeInputState.IsAltDown);
        if (shortcut is not { } action)
        {
            return false;
        }

        if (action == TerminalShortcut.Paste)
        {
            if (!ClipboardHasText())
            {
                return false;
            }

            _ = PasteAsync();
        }
        else
        {
            Copy();
        }

        _handledKeys.Add(virtualKey);
        _swallowChar = virtualKey is VkC or VkV;
        return true;
    }

    private static bool ClipboardHasText()
    {
        try
        {
            return Clipboard.GetContent().Contains(StandardDataFormats.Text);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            return false;
        }
    }

    private async Task PasteAsync()
    {
        try
        {
            var text = await Clipboard.GetContent().GetTextAsync();
            if (!string.IsNullOrEmpty(text))
            {
                _send(PasteEncoder.Encode(text, _state.UsesBracketedPaste));
            }
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
        }
    }

    private void Copy()
    {
        try
        {
            var text = _selectedText();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
            Clipboard.Flush();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
        }
    }
}