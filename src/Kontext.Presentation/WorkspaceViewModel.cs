using System.ComponentModel;

namespace Kontext.Presentation;

public sealed class WorkspaceViewModel
{
    private readonly SelectionAutoStart _autoStart;

    public WorkspaceViewModel(IssueSidebarViewModel sidebar, IssueDetailViewModel detail, SelectionAutoStart autoStart)
    {
        Sidebar = sidebar;
        Detail = detail;
        _autoStart = autoStart;

        sidebar.PropertyChanged += OnSidebarPropertyChanged;
    }

    public IssueSidebarViewModel Sidebar { get; }

    public IssueDetailViewModel Detail { get; }

    private void OnSidebarPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IssueSidebarViewModel.SelectedItem))
        {
            var item = Sidebar.SelectedItem;
            Detail.Item = item;
            _autoStart.OnSelected(item?.Issue);
        }
    }
}
