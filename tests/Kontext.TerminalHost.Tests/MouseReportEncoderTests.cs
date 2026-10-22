namespace Kontext.TerminalHost.Tests;

[TestFixture]
public class MouseReportEncoderTests
{
    [Test]
    public void ReportsALeftPressInTheSgrFormat()
    {
        var report = MouseReportEncoder.Encode(MouseButton.Left, MouseAction.Press, 12, 5, MouseModifiers.None, sgr: true);

        Assert.That(report, Is.EqualTo("\u001b[<0;12;5M"));
    }

    [Test]
    public void ReportsAReleaseWithTheSameButtonAndALowercaseEnding()
    {
        var report = MouseReportEncoder.Encode(MouseButton.Left, MouseAction.Release, 12, 5, MouseModifiers.None, sgr: true);

        Assert.That(report, Is.EqualTo("\u001b[<0;12;5m"));
    }

    [TestCase(MouseButton.Left, "\u001b[<0;3;4M")]
    [TestCase(MouseButton.Middle, "\u001b[<1;3;4M")]
    [TestCase(MouseButton.Right, "\u001b[<2;3;4M")]
    public void NumbersEachButton(MouseButton button, string expected)
    {
        Assert.That(MouseReportEncoder.Encode(button, MouseAction.Press, 3, 4, MouseModifiers.None, sgr: true), Is.EqualTo(expected));
    }

    [Test]
    public void AddsTheHeldKeysToTheButtonCode()
    {
        var report = MouseReportEncoder.Encode(MouseButton.Left, MouseAction.Press, 1, 1, MouseModifiers.Control | MouseModifiers.Alt, sgr: true);

        Assert.That(report, Is.EqualTo("\u001b[<24;1;1M"), "0 + 16 (ctrl) + 8 (alt)");
    }

    [Test]
    public void MarksMovementWithAHeldButton()
    {
        var report = MouseReportEncoder.Encode(MouseButton.Left, MouseAction.Move, 7, 2, MouseModifiers.None, sgr: true);

        Assert.That(report, Is.EqualTo("\u001b[<32;7;2M"));
    }

    [Test]
    public void MarksMovementWithNoButtonAsButtonThreePlusMotion()
    {
        var report = MouseReportEncoder.Encode(MouseButton.None, MouseAction.Move, 7, 2, MouseModifiers.None, sgr: true);

        Assert.That(report, Is.EqualTo("\u001b[<35;7;2M"));
    }

    [Test]
    public void WritesTheLegacyFormatWithEachNumberAsOneCharacterOffsetBy32()
    {
        var report = MouseReportEncoder.Encode(MouseButton.Left, MouseAction.Press, 10, 20, MouseModifiers.None, sgr: false);

        Assert.That(report, Is.EqualTo("\u001b[M" + (char)32 + (char)42 + (char)52));
    }

    [Test]
    public void TheLegacyFormatCannotSayWhichButtonWasReleased()
    {
        var report = MouseReportEncoder.Encode(MouseButton.Right, MouseAction.Release, 1, 1, MouseModifiers.None, sgr: false);

        Assert.That(report, Is.EqualTo("\u001b[M" + (char)35 + (char)33 + (char)33));
    }

    [Test]
    public void GivesUpOnPositionsTheLegacyFormatCannotHold()
    {
        Assert.That(MouseReportEncoder.Encode(MouseButton.Left, MouseAction.Press, 224, 1, MouseModifiers.None, sgr: false), Is.Null);
        Assert.That(MouseReportEncoder.Encode(MouseButton.Left, MouseAction.Press, 1, 224, MouseModifiers.None, sgr: false), Is.Null);
    }

    [Test]
    public void TheSgrFormatHasNoSuchLimit()
    {
        var report = MouseReportEncoder.Encode(MouseButton.Left, MouseAction.Press, 500, 300, MouseModifiers.None, sgr: true);

        Assert.That(report, Is.EqualTo("\u001b[<0;500;300M"));
    }

    [Test]
    public void CellsAreCountedFromOne()
    {
        Assert.That(() => MouseReportEncoder.Encode(MouseButton.Left, MouseAction.Press, 0, 1, MouseModifiers.None, sgr: true), Throws.InstanceOf<ArgumentOutOfRangeException>());
        Assert.That(() => MouseReportEncoder.Encode(MouseButton.Left, MouseAction.Press, 1, 0, MouseModifiers.None, sgr: true), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }
}
