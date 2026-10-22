
namespace Kontext.Presentation.Tests;

[TestFixture]
public class AppLifetimeTests
{
    [Test]
    public void ClosingTheWindowHidesItUntilExitIsRequested()
    {
        var lifetime = new AppLifetime();

        Assert.That(lifetime.ClosingHidesWindow, Is.True);
        Assert.That(lifetime.ExitRequested, Is.False);
    }

    [Test]
    public void AfterExitIsRequestedClosingTheWindowEndsTheApplication()
    {
        var lifetime = new AppLifetime();

        lifetime.RequestExit();

        Assert.That(lifetime.ExitRequested, Is.True);
        Assert.That(lifetime.ClosingHidesWindow, Is.False);
    }
}
