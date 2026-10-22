namespace Kontext.TerminalHost.Tests;

[TestFixture]
public class MouseReportingStateTests
{
    private const string Esc = "\u001b";

    private MouseReportingState _state = null!;

    [SetUp]
    public void CreateState() => _state = new MouseReportingState();

    [Test]
    public void StartsWithTheMouseBelongingToTheTerminal()
    {
        Assert.That(_state.Tracking, Is.EqualTo(MouseTracking.Off));
        Assert.That(_state.UsesSgrEncoding, Is.False);
    }

    [TestCase("1000", MouseTracking.Press)]
    [TestCase("1002", MouseTracking.Drag)]
    [TestCase("1003", MouseTracking.AnyMotion)]
    public void FollowsTheModeTheProgramTurnsOn(string mode, MouseTracking expected)
    {
        _state.Observe($"{Esc}[?{mode}h");

        Assert.That(_state.Tracking, Is.EqualTo(expected));
    }

    [Test]
    public void FollowsTheModeBeingTurnedOffAgain()
    {
        _state.Observe($"{Esc}[?1000h");
        _state.Observe($"{Esc}[?1000l");

        Assert.That(_state.Tracking, Is.EqualTo(MouseTracking.Off));
    }

    [Test]
    public void ReadsSeveralModesInOneSequence()
    {
        _state.Observe($"{Esc}[?1002;1006h");

        Assert.That(_state.Tracking, Is.EqualTo(MouseTracking.Drag));
        Assert.That(_state.UsesSgrEncoding, Is.True);
    }

    [Test]
    public void TheMostDemandingModeThatIsOnWins()
    {
        _state.Observe($"{Esc}[?1000h{Esc}[?1003h");
        Assert.That(_state.Tracking, Is.EqualTo(MouseTracking.AnyMotion));

        _state.Observe($"{Esc}[?1003l");
        Assert.That(_state.Tracking, Is.EqualTo(MouseTracking.Press), "turning one off leaves the others");
    }

    [Test]
    public void FindsSequencesAmongOrdinaryOutput()
    {
        _state.Observe($"Welcome {Esc}[1;32mgreen{Esc}[0m text{Esc}[?1000h{Esc}[?1006h more text");

        Assert.That(_state.Tracking, Is.EqualTo(MouseTracking.Press));
        Assert.That(_state.UsesSgrEncoding, Is.True);
    }

    [Test]
    public void PutsASequenceSplitBetweenTwoPiecesOfOutputBackTogether()
    {
        _state.Observe($"text{Esc}[?10");
        Assert.That(_state.Tracking, Is.EqualTo(MouseTracking.Off), "not yet known");

        _state.Observe("00h more");

        Assert.That(_state.Tracking, Is.EqualTo(MouseTracking.Press));
    }

    [Test]
    public void PutsTogetherASequenceSplitRightAfterTheEscape()
    {
        _state.Observe(Esc);
        _state.Observe("[?1003h");

        Assert.That(_state.Tracking, Is.EqualTo(MouseTracking.AnyMotion));
    }

    [Test]
    public void IgnoresOtherPrivateModes()
    {
        _state.Observe($"{Esc}[?25l{Esc}[?1049h{Esc}[?2004h");

        Assert.That(_state.Tracking, Is.EqualTo(MouseTracking.Off));
    }

    [Test]
    public void IgnoresOtherSequencesThatLookSimilar()
    {
        _state.Observe($"{Esc}[1000h{Esc}]1000h{Esc}[?1000;");

        Assert.That(_state.Tracking, Is.EqualTo(MouseTracking.Off));
    }

    [Test]
    public void ADroppedUnfinishedSequenceDoesNotBreakTheNextOne()
    {
        _state.Observe($"{Esc}[?10");
        _state.Observe($"x{Esc}[?1000h");

        Assert.That(_state.Tracking, Is.EqualTo(MouseTracking.Press));
    }

    [Test]
    public void AFullResetEndsMouseReporting()
    {
        _state.Observe($"{Esc}[?1003h{Esc}[?1006h");

        _state.Observe($"{Esc}c");

        Assert.That(_state.Tracking, Is.EqualTo(MouseTracking.Off));
        Assert.That(_state.UsesSgrEncoding, Is.False);
    }

    [Test]
    public void ManyPiecesOfPlainOutputChangeNothing()
    {
        for (var i = 0; i < 1000; i++)
        {
            _state.Observe("line of ordinary output\r\n");
        }

        Assert.That(_state.Tracking, Is.EqualTo(MouseTracking.Off));
    }
}
