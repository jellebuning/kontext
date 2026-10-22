using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Contracts;

public interface IIssueWorkTracker
{
    event EventHandler<IssueWorkChangedEventArgs>? Changed;

    IssueWorkState StateOf(IssueKey issue);

    void Report(IssueKey issue, IssueWorkState state);
}

public sealed class IssueWorkChangedEventArgs(IssueKey issue, IssueWorkState state) : EventArgs
{
    public IssueKey Issue { get; } = issue;

    public IssueWorkState State { get; } = state;
}
