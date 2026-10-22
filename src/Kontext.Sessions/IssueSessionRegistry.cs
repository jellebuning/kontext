using System.Collections.Concurrent;
using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Sessions;

internal sealed class IssueSessionRegistry : IIssueSessionRegistry
{
    private readonly ConcurrentDictionary<IssueKey, ITerminalSession> _sessions = new();

    public event EventHandler<IssueSessionChangedEventArgs>? Changed;

    public ITerminalSession? Find(IssueKey issue) => _sessions.GetValueOrDefault(issue);

    public IssueKey? IssueOf(TerminalSessionId session)
    {
        foreach (var (issue, registered) in _sessions)
        {
            if (registered.Id == session)
            {
                return issue;
            }
        }

        return null;
    }

    public void Register(IssueKey issue, ITerminalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _sessions[issue] = session;
        Changed?.Invoke(this, new IssueSessionChangedEventArgs(issue, session));
    }

    public void Remove(IssueKey issue) => _sessions.TryRemove(issue, out _);
}
