using Kontext.Domain;

namespace Kontext.ExternalTools;

internal sealed class TerminalTool(IProcessStarter processes) : IWorkspaceTool
{
    public WorkspaceTool Tool => WorkspaceTool.Terminal;

    public void Open(string folder)
    {
        try
        {
            processes.Start(new ProcessLaunch("wt.exe", ["-d", folder]));
        }
        catch (ExternalToolException)
        {
            processes.Start(new ProcessLaunch("powershell.exe", ["-NoExit"], WorkingDirectory: folder));
        }
    }
}
