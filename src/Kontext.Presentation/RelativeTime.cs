using System.Globalization;

namespace Kontext.Presentation;

internal static class RelativeTime
{
    public static string Format(DateTimeOffset moment, DateTimeOffset now)
    {
        var age = now - moment;

        return age switch
        {
            { TotalMinutes: < 1 } => "just now",
            { TotalHours: < 1 } => $"{(int)age.TotalMinutes}m ago",
            { TotalDays: < 1 } => $"{(int)age.TotalHours}h ago",
            { TotalDays: < 30 } => $"{(int)age.TotalDays}d ago",
            _ => moment.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture),
        };
    }
}
