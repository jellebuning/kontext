namespace Kontext.Presentation;

public sealed record AutoStartOptions(TimeSpan Delay)
{
    public static AutoStartOptions Default { get; } = new(TimeSpan.FromSeconds(1));
}
