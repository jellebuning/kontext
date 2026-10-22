namespace Kontext.Tray;

internal sealed class MenuTheme
{
    private bool _applied;

    public void FollowSystem()
    {
        if (_applied)
        {
            return;
        }

        _applied = true;

        try
        {
            _ = NativeMethods.SetPreferredAppMode(NativeMethods.AllowDarkAppMode);
            NativeMethods.FlushMenuThemes();
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
        }
    }
}
