namespace Kontext.TerminalHost.UnitTests;

[TestFixture]
public class TerminalShortcutReaderTests
{
    private const int VkC = 0x43;
    private const int VkV = 0x56;
    private const int VkInsert = 0x2D;

    [TestCase(VkV, true, false, TerminalShortcut.Paste, Description = "Ctrl+V")]
    [TestCase(VkV, true, true, TerminalShortcut.Paste, Description = "Ctrl+Shift+V")]
    [TestCase(VkInsert, false, true, TerminalShortcut.Paste, Description = "Shift+Insert")]
    [TestCase(VkInsert, true, false, TerminalShortcut.Copy, Description = "Ctrl+Insert")]
    [TestCase(VkC, true, true, TerminalShortcut.Copy, Description = "Ctrl+Shift+C")]
    public void RecognisesTheClipboardShortcuts(int virtualKey, bool control, bool shift, TerminalShortcut expected)
    {
        Assert.That(TerminalShortcutReader.FromKey(virtualKey, control, shift, alt: false), Is.EqualTo(expected));
    }

    [Test]
    public void PlainControlCIsLeftAloneSoItStillInterrupts()
    {
        Assert.That(TerminalShortcutReader.FromKey(VkC, control: true, shift: false, alt: false), Is.Null);
    }

    [Test]
    public void KeysWithoutTheirModifiersAreLeftAlone()
    {
        Assert.That(TerminalShortcutReader.FromKey(VkV, control: false, shift: false, alt: false), Is.Null, "V");
        Assert.That(TerminalShortcutReader.FromKey(VkV, control: false, shift: true, alt: false), Is.Null, "Shift+V");
        Assert.That(TerminalShortcutReader.FromKey(VkC, control: false, shift: false, alt: false), Is.Null, "C");
        Assert.That(TerminalShortcutReader.FromKey(VkC, control: false, shift: true, alt: false), Is.Null, "Shift+C");
        Assert.That(TerminalShortcutReader.FromKey(VkInsert, control: false, shift: false, alt: false), Is.Null, "Insert");
    }

    [Test]
    public void ControlShiftInsertIsLeftAlone()
    {
        Assert.That(TerminalShortcutReader.FromKey(VkInsert, control: true, shift: true, alt: false), Is.Null);
    }

    [TestCase(VkV, true, false)]
    [TestCase(VkV, true, true)]
    [TestCase(VkInsert, false, true)]
    [TestCase(VkInsert, true, false)]
    [TestCase(VkC, true, true)]
    public void AnythingWithAltIsLeftAlone(int virtualKey, bool control, bool shift)
    {
        Assert.That(TerminalShortcutReader.FromKey(virtualKey, control, shift, alt: true), Is.Null);
    }

    [Test]
    public void AltGrIsLeftAloneBecauseWindowsReportsItAsControlPlusAlt()
    {
        Assert.That(TerminalShortcutReader.FromKey(VkV, control: true, shift: false, alt: true), Is.Null);
    }

    [Test]
    public void OtherKeysAreLeftAlone()
    {
        Assert.That(TerminalShortcutReader.FromKey('A', control: true, shift: true, alt: false), Is.Null);
        Assert.That(TerminalShortcutReader.FromKey(0x0D, control: true, shift: true, alt: false), Is.Null, "Enter");
    }
}
