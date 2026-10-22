using Kontext.Domain;

namespace Kontext.ExternalTools;

internal sealed class ExplorerTool(IProcessStarter processes) : IWorkspaceTool
{
    public WorkspaceTool Tool => WorkspaceTool.Explorer;

    public void Open(string folder) => processes.Start(new ProcessLaunch("explorer.exe", [folder]));
}
