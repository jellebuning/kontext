using Kontext.Kernel;

namespace Kontext.Domain;

public sealed record StoredSession(IssueKey Issue, TerminalSessionId SessionId, string WorktreePath, string BranchName);
