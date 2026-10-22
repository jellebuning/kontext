namespace Kontext.Domain;

public sealed record IssueWorkState(
    IssueWorkStatus Status,
    string? BranchName = null,
    string? WorktreePath = null,
    string? Message = null,
    bool SessionEnded = false)
{
    public static IssueWorkState NotStarted { get; } = new(IssueWorkStatus.NotStarted);
}
