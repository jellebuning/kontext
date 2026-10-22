using Kontext.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Views;

public sealed partial class IssueStatusBadge : UserControl
{
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status),
        typeof(IssueWorkStatus),
        typeof(IssueStatusBadge),
        new PropertyMetadata(IssueWorkStatus.NotStarted, (badge, _) => ((IssueStatusBadge)badge).Update()));

    public IssueStatusBadge()
    {
        InitializeComponent();
        Loaded += (_, _) => Update();
    }

    public IssueWorkStatus Status
    {
        get => (IssueWorkStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    private void Update() => VisualStateManager.GoToState(this, Status.ToString(), useTransitions: false);
}
