using System.Collections.ObjectModel;

namespace Kontext.Presentation;

public sealed class RepositoryGroupViewModel(string name, IEnumerable<IssueItemViewModel> items)
{
    public string Name { get; } = name;

    public ObservableCollection<IssueItemViewModel> Items { get; } = new(items);


    public bool IsExpanded { get; set; } = true;
}
