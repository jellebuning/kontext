namespace Kontext.Domain;

public sealed record TerminalSessionOptions(string WorkingDirectory, string Command, IReadOnlyList<string> Arguments);
