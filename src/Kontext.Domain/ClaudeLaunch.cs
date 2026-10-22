namespace Kontext.Domain;

public sealed record ClaudeLaunch(TerminalSessionId SessionId, TerminalSessionOptions Options, bool ReportsItself, bool Resumed);
