namespace Kontext.Claude;

public sealed record ClaudeOptions(string ConfigDirectory, string Command = "claude", string PermissionMode = "acceptEdits");
