namespace Kontext.TerminalHost.UnitTests;

[TestFixture]
public class ZoomGestureReaderTests
{
    private ZoomGestureReader _reader = null!;

    [SetUp]
    public void CreateReader() => _reader = new ZoomGestureReader();

    [TestCase(0xBB, ZoomStep.In, Description = "= / + on the main row")]
    [TestCase(0x6B, ZoomStep.In, Description = "+ on the number pad")]
    [TestCase(0xBD, ZoomStep.Out, Description = "- on the main row")]
    [TestCase(0x6D, ZoomStep.Out, Description = "- on the number pad")]
    [TestCase(0x30, ZoomStep.Reset, Description = "0 on the main row")]
    [TestCase(0x60, ZoomStep.Reset, Description = "0 on the number pad")]
    public void RecognisesTheZoomKeys(int virtualKey, ZoomStep expected)
    {
        Assert.That(ZoomGestureReader.FromKey(virtualKey), Is.EqualTo(expected));
    }

    [Test]
    public void IgnoresOtherKeys()
    {
        Assert.That(ZoomGestureReader.FromKey('A'), Is.Null);
        Assert.That(ZoomGestureReader.FromKey(0x0D), Is.Null, "Enter");
    }

    [Test]
    public void AWheelNotchUpZoomsInAndDownZoomsOut()
    {
        Assert.That(_reader.FromWheel(120), Is.EqualTo(ZoomStep.In));
        Assert.That(_reader.FromWheel(-120), Is.EqualTo(ZoomStep.Out));
    }

    [Test]
    public void SmallTouchpadMovementsAddUpToOneStep()
    {
        var steps = Enumerable.Range(0, 5).Select(_ => _reader.FromWheel(30)).ToList();

        Assert.That(steps, Is.EqualTo(new ZoomStep?[] { null, null, null, ZoomStep.In, null }));
    }

    [Test]
    public void ABigWheelMovementIsStillOneStep()
    {
        Assert.That(_reader.FromWheel(360), Is.EqualTo(ZoomStep.In));
        Assert.That(_reader.FromWheel(30), Is.Null, "the extra is not kept");
    }

    [Test]
    public void MovementInTheOppositeDirectionCancelsWhatWasBuiltUp()
    {
        _reader.FromWheel(90);

        Assert.That(_reader.FromWheel(-60), Is.Null);
        Assert.That(_reader.FromWheel(-150), Is.EqualTo(ZoomStep.Out));
    }
}
