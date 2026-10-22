using System.Text.Json;

namespace Kontext.Claude;

internal static class ClaudeHookPayload
{
    public static (string Name, string? NotificationType)? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var name = StringProperty(document.RootElement, "hook_event_name");
            return string.IsNullOrWhiteSpace(name) ? null : (name, StringProperty(document.RootElement, "notification_type"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? StringProperty(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
