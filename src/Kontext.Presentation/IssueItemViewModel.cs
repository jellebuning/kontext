using CommunityToolkit.Mvvm.ComponentModel;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Presentation;

public sealed partial class IssueItemViewModel : ObservableObject
{
    public IssueItemViewModel(GitHubIssue issue, IssueWorkState state, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(state);

        Issue = issue;
        Key = issue.Key;
        State = state;
        Subtitle = BuildSubtitle(issue, now);
    }

    public GitHubIssue Issue { get; }

    public IssueKey Key { get; }

    public string Title => Issue.Title;

    public string NumberText => $"#{Issue.Number}";

    public string Subtitle { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status), nameof(StatusText))]
    public partial IssueWorkState State { get; set; }

    public IssueWorkStatus Status => Issue.IsClosed && State.Status is IssueWorkStatus.Ready or IssueWorkStatus.NeedsInput ? IssueWorkStatus.Done : State.Status;

    public string StatusText => IssueStatusText.For(Status);

    private static string BuildSubtitle(GitHubIssue issue, DateTimeOffset now)
    {
        var subtitle = $"updated {RelativeTime.Format(issue.UpdatedAt, now)}";
        return issue.Author is null ? subtitle : $"{subtitle} · by {issue.Author}";
    }
}
