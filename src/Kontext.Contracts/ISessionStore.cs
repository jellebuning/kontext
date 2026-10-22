using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Contracts;

public interface ISessionStore
{
    StoredSession? Find(IssueKey issue);

    void Save(StoredSession session);

    void Remove(IssueKey issue);
}
