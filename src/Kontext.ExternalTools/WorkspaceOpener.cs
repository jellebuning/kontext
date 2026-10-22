using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.ExternalTools;

internal sealed class WorkspaceOpener(IEnumerable<IWorkspaceTool> tools, IFolderFiles files) : IWorkspaceOpener
{
    public void Open(WorkspaceTool tool, string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        if (!files.Exists(folder))
        {
            throw new ExternalToolException($"The folder '{folder}' no longer exists.");
        }

        var opener = tools.FirstOrDefault(candidate => candidate.Tool == tool)
            ?? throw new ExternalToolException($"There is no way to open a workspace in {tool}.");

        opener.Open(folder);
    }
}
