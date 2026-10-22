using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Presentation;

public sealed partial class IssueSidebarViewModel : ObservableObject, IDisposable
{
    private readonly IGitHubService _gitHub;
    private readonly IIssueWorkTracker _tracker;
    private readonly IExistingWorkspaceFinder _existingWorkspaces;
    private readonly IWorkspaceIssues _workspaceIssues;
    private readonly TimeProvider _clock;
    private readonly LatestOnly _loads = new();
    private readonly Dictionary<string, RepositoryGroupViewModel> _groupsByName = new(StringComparer.OrdinalIgnoreCase);

    private List<IssueItemViewModel> _all = [];
    private Dictionary<IssueKey, IssueItemViewModel> _itemsByKey = [];
    private bool _loadedOnce;

    public IssueSidebarViewModel(
        IGitHubService gitHub,
        IIssueWorkTracker tracker,
        IExistingWorkspaceFinder existingWorkspaces,
        IWorkspaceIssues workspaceIssues,
        TimeProvider clock)
    {
        _gitHub = gitHub;
        _tracker = tracker;
        _existingWorkspaces = existingWorkspaces;
        _workspaceIssues = workspaceIssues;
        _clock = clock;

        _tracker.Changed += OnTrackerChanged;
    }

    public ObservableCollection<RepositoryGroupViewModel> Groups { get; } = [];

    [ObservableProperty]
    public partial IssueItemViewModel? SelectedItem { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string CountText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(IsEmpty))]
    public partial string? ErrorMessage { get; set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsEmpty => _loadedOnce && !IsLoading && !HasError && _all.Count == 0;

    public void Dispose()
    {
        _tracker.Changed -= OnTrackerChanged;
        _loads.Dispose();
    }

    public void Clear()
    {
        _loads.CancelCurrent();

        _all = [];
        _itemsByKey = [];
        _groupsByName.Clear();
        Groups.Clear();

        _loadedOnce = false;

        SelectedItem = null;
        SearchText = string.Empty;
        IsLoading = false;
        CountText = string.Empty;
        ErrorMessage = null;
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RefreshAsync()
    {
        var load = _loads.Begin();
        var token = load.Token;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var open = await _gitHub.GetIssuesAsync(repository: null, token);
            var workspaces = await _existingWorkspaces.FindAllAsync(token);

            var withWorkspace = await _workspaceIssues.FindMissingAsync(open, workspaces, token);
            token.ThrowIfCancellationRequested();

            Show([.. open, .. withWorkspace]);
            MarkExistingWorkspaces(workspaces);
            _loadedOnce = true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            ErrorMessage = ex.Message;
            _all = [];
            _itemsByKey = [];
            ApplyFilter();
        }

        if (_loads.IsCurrent(load))
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void Show(IReadOnlyList<GitHubIssue> issues)
    {
        var now = _clock.GetUtcNow();
        var previouslySelected = SelectedItem?.Key;

        _all = issues.DistinctBy(issue => issue.Key).Select(issue => new IssueItemViewModel(issue, _tracker.StateOf(issue.Key), now)).ToList();
        _itemsByKey = _all.ToDictionary(item => item.Key);

        SelectedItem = previouslySelected is { } key && _itemsByKey.TryGetValue(key, out var reloaded) ? reloaded : null;

        ApplyFilter();
    }

    private void MarkExistingWorkspaces(IReadOnlyList<ExistingWorkspace> workspaces)
    {
        foreach (var workspace in workspaces)
        {
            if (!_itemsByKey.TryGetValue(workspace.Issue, out var item))
            {
                continue;
            }

            var status = _tracker.StateOf(workspace.Issue).Status;
            var closed = item.Issue.IsClosed;

            var next = (status, closed) switch
            {
                (IssueWorkStatus.NotStarted or IssueWorkStatus.Ready, true) => IssueWorkStatus.Done,
                (IssueWorkStatus.NotStarted, false) => IssueWorkStatus.Ready,
                _ => status,
            };

            if (next != status)
            {
                _tracker.Report(workspace.Issue, new IssueWorkState(next, workspace.Worktree.BranchName, workspace.Worktree.Path));
            }
        }
    }

    private void ApplyFilter()
    {
        var visible = _all.Where(item => IssueSearch.Matches(item.Issue, SearchText)).ToList();

        var groups = IssueGrouper.Group(visible, item => item.Issue.Repository.FullName, item => item.Issue.UpdatedAt);

        Groups.Clear();
        foreach (var group in groups)
        {
            if (!_groupsByName.TryGetValue(group.Repository, out var viewModel))
            {
                viewModel = new RepositoryGroupViewModel(group.Repository, group.Items);
                _groupsByName[group.Repository] = viewModel;
            }
            else
            {
                viewModel.Items.Clear();
                foreach (var item in group.Items)
                {
                    viewModel.Items.Add(item);
                }
            }

            Groups.Add(viewModel);
        }

        CountText = BuildCountText(visible.Count);
        OnPropertyChanged(nameof(IsEmpty));
    }

    private string BuildCountText(int visibleCount)
    {
        if (_all.Count == 0)
        {
            return string.Empty;
        }

        var closed = _all.Count(item => item.Issue.IsClosed);
        var open = _all.Count - closed;

        var issues = open == 1 ? "1 open issue" : $"{open} open issues";
        var text = string.IsNullOrWhiteSpace(SearchText) ? issues : $"{visibleCount} of {issues}";

        return closed == 0 ? text : $"{text} · {closed} closed";
    }

    private void OnTrackerChanged(object? sender, IssueWorkChangedEventArgs e)
    {
        if (!_itemsByKey.TryGetValue(e.Issue, out var item))
        {
            return;
        }

        item.State = e.State;

        if (item.Issue.IsClosed && e.State.Status == IssueWorkStatus.NotStarted)
        {
            Remove(item);
        }
    }

    private void Remove(IssueItemViewModel item)
    {
        if (ReferenceEquals(SelectedItem, item))
        {
            SelectedItem = null;
        }

        _all.Remove(item);
        _itemsByKey.Remove(item.Key);
        ApplyFilter();
    }
}
