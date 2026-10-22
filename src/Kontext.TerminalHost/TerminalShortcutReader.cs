namespace Kontext.TerminalHost;

public static class TerminalShortcutReader
{
    private const int VkInsert = 0x2D;
    private const int VkC = 0x43;
    private const int VkV = 0x56;

    public static TerminalShortcut? FromKey(int virtualKey, bool control, bool shift, bool alt)
    {
        if (alt)
        {
            return null;
        }

        return virtualKey switch
        {
            VkV when control => TerminalShortcut.Paste,
            VkInsert when shift && !control => TerminalShortcut.Paste,
            VkInsert when control && !shift => TerminalShortcut.Copy,
            VkC when control && shift => TerminalShortcut.Copy,
            _ => null,
        };
    }
}
