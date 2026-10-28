namespace Kontext.TerminalHost.UnitTests;

[TestFixture]
public class PasteEncoderTests
{
    private const string Esc = "\u001b";

    [TestCase("a\r\nb", "a\rb", Description = "CRLF")]
    [TestCase("a\nb", "a\rb", Description = "lone LF")]
    [TestCase("a\rb", "a\rb", Description = "lone CR")]
    [TestCase("a\r\n\r\nb", "a\r\rb", Description = "two CRLF")]
    [TestCase("a\n\nb", "a\r\rb", Description = "two LF")]
    [TestCase("a\r\rb", "a\r\rb", Description = "two CR")]
    [TestCase("a\n\rb", "a\r\rb", Description = "LF then CR are two line breaks")]
    [TestCase("line\r\n", "line\r", Description = "trailing CRLF")]
    public void EveryKindOfLineBreakBecomesOneEnter(string text, string expected)
    {
        Assert.That(PasteEncoder.Encode(text, bracketed: false), Is.EqualTo(expected));
    }

    [Test]
    public void LeavesOrdinaryTextAndTabsAsTheyAre()
    {
        Assert.That(PasteEncoder.Encode("git commit -m \"x\"\tz", bracketed: false), Is.EqualTo("git commit -m \"x\"\tz"));
    }

    [Test]
    public void RemovesEscapeCharacters()
    {
        Assert.That(PasteEncoder.Encode($"a{Esc}[31mb{Esc}", bracketed: false), Is.EqualTo("a[31mb"));
    }

    [Test]
    public void PastedTextCannotEndTheBracketedPasteEarly()
    {
        var result = PasteEncoder.Encode($"rm -rf x{Esc}[201~; evil", bracketed: true);

        Assert.That(result, Is.EqualTo($"{Esc}[200~rm -rf x[201~; evil{Esc}[201~"));
        Assert.That(result.IndexOf($"{Esc}[201~", StringComparison.Ordinal), Is.EqualTo(result.Length - 6), "only the closing marker is left");
    }

    [Test]
    public void WrapsTheTextInMarkersWhenBracketedPasteIsOn()
    {
        Assert.That(PasteEncoder.Encode("a\r\nb", bracketed: true), Is.EqualTo($"{Esc}[200~a\rb{Esc}[201~"));
    }

    [Test]
    public void AddsNoMarkersWhenBracketedPasteIsOff()
    {
        Assert.That(PasteEncoder.Encode("abc", bracketed: false), Is.EqualTo("abc"));
    }

    [Test]
    public void EmptyTextGivesNothingWithoutBracketedPaste()
    {
        Assert.That(PasteEncoder.Encode(string.Empty, bracketed: false), Is.EqualTo(string.Empty));
    }

    [Test]
    public void EmptyTextGivesJustTheMarkersWithBracketedPaste()
    {
        Assert.That(PasteEncoder.Encode(string.Empty, bracketed: true), Is.EqualTo($"{Esc}[200~{Esc}[201~"));
    }

    [Test]
    public void NullTextIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => PasteEncoder.Encode(null!, bracketed: false));
    }
}
