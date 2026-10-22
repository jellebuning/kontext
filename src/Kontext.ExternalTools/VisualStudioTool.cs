using Kontext.Domain;

namespace Kontext.ExternalTools;

internal sealed class VisualStudioTool(IFolderFiles files, IVisualStudioLocator locator, IProcessStarter processes) : IWorkspaceTool
{
    private static readonly string[] SolutionPatterns = ["*.slnx", "*.sln"];

    public WorkspaceTool Tool => WorkspaceTool.VisualStudio;

    public void Open(string folder)
    {
        var solution = SolutionPatterns.SelectMany(pattern => files.Find(folder, pattern)).FirstOrDefault();
        if (solution is not null)
        {
            processes.Start(new ProcessLaunch(solution, [], UseShellExecute: true));
            return;
        }

        var devenv = locator.FindDevenv()
            ?? throw new ExternalToolException("Visual Studio was not found on this computer.");

        processes.Start(new ProcessLaunch(devenv, [folder]));
    }
}
