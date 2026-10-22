namespace Kontext.Presentation;

public sealed class AppLifetime
{
    public bool ExitRequested { get; private set; }

    public bool ClosingHidesWindow => !ExitRequested;

    public void RequestExit() => ExitRequested = true;
}
