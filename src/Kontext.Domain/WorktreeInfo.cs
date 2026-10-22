namespace Kontext.Domain;

public sealed record WorktreeInfo(string Path, string BranchName, string? HeadCommit, bool IsMain);
