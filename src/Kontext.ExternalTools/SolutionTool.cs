using Kontext.Domain;

namespace Kontext.ExternalTools;

internal sealed class SolutionTool(IFolderFiles files, IProcessStarter processes) : IWorkspaceTool
{
    private static readonly string[] SolutionPatterns = ["*.slnx", "*.sln"];

    public WorkspaceTool Tool => WorkspaceTool.Solution;

    public void Open(string folder)
    {
        var solution = SolutionPatterns.SelectMany(pattern => files.Find(folder, pattern)).FirstOrDefault()
            ?? throw new ExternalToolException("No solution file (.slnx or .sln) was found in this folder.");

        processes.Start(new ProcessLaunch(solution, [], UseShellExecute: true));
    }
}
