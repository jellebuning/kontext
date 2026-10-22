using Kontext.Domain;

namespace Kontext.Presentation;

public static class IssueStatusText
{
    public static string For(IssueWorkStatus status) => status switch
    {
        IssueWorkStatus.NotStarted => "Not started",
        IssueWorkStatus.Preparing => "Preparing…",
        IssueWorkStatus.Ready => "Environment ready",
        IssueWorkStatus.Running => "Running",
        IssueWorkStatus.NeedsInput => "Needs input",
        IssueWorkStatus.Done => "Done",
        IssueWorkStatus.Failed => "Failed",
        _ => status.ToString(),
    };
}
