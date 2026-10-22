using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Kontext.Presentation;

public sealed partial class WorkspaceViewModel : ObservableObject
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetailVisible))]
    public partial bool IsSettingsOpen { get; set; }

    public bool IsDetailVisible => !IsSettingsOpen;

    private void OnSidebarPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IssueSidebarViewModel.SelectedItem))
        {
            var item = Sidebar.SelectedItem;
            if (item is not null && item.Key != Detail.Item?.Key)
            {
                IsSettingsOpen = false;
            }

            Detail.Item = item;
            _autoStart.OnSelected(item?.Issue);
        }
    }
}
