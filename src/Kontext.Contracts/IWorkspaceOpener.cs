using Kontext.Domain;

namespace Kontext.Contracts;

public interface IWorkspaceOpener
{
    void Open(WorkspaceTool tool, string folder);
}
