namespace Kontext.TerminalHost.Tests;

[TestFixture]
public class TerminalZoomTests
{
    private TerminalZoom _zoom = null!;
    private int _changes;

    [SetUp]
    public void CreateZoom()
    {
        _zoom = new TerminalZoom();
        _changes = 0;
        _zoom.Changed += (_, _) => _changes++;
    }

    [Test]
    public void StartsAtTheDefaultSize()
    {
        Assert.That(_zoom.FontSize, Is.EqualTo(TerminalZoom.DefaultSize));
    }

    [Test]
    public void ZoomsInAndOutByOnePoint()
    {
        _zoom.Apply(ZoomStep.In);
        _zoom.Apply(ZoomStep.In);
        _zoom.Apply(ZoomStep.Out);

        Assert.That(_zoom.FontSize, Is.EqualTo(TerminalZoom.DefaultSize + 1));
        Assert.That(_changes, Is.EqualTo(3));
    }

    [Test]
    public void ResetGoesBackToTheDefault()
    {
        _zoom.Apply(ZoomStep.In);
        _zoom.Apply(ZoomStep.In);

        _zoom.Apply(ZoomStep.Reset);

        Assert.That(_zoom.FontSize, Is.EqualTo(TerminalZoom.DefaultSize));
    }

    [Test]
    public void StopsAtTheLargestSizeWithoutSayingAnythingChanged()
    {
        for (var i = 0; i < 100; i++)
        {
            _zoom.Apply(ZoomStep.In);
        }

        var changesAtTheLimit = _changes;
        _zoom.Apply(ZoomStep.In);

        Assert.That(_zoom.FontSize, Is.EqualTo(TerminalZoom.MaximumSize));
        Assert.That(_changes, Is.EqualTo(changesAtTheLimit));
    }

    [Test]
    public void StopsAtTheSmallestSize()
    {
        for (var i = 0; i < 100; i++)
        {
            _zoom.Apply(ZoomStep.Out);
        }

        Assert.That(_zoom.FontSize, Is.EqualTo(TerminalZoom.MinimumSize));
    }

    [Test]
    public void ResettingAtTheDefaultSaysNothing()
    {
        _zoom.Apply(ZoomStep.Reset);

        Assert.That(_changes, Is.Zero);
    }
}
