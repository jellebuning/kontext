using Kontext.Kernel;

namespace Kontext.GitAutomation;

public interface IWorktreePathProvider
{
    string GetPath(RepositoryRef repository, string branchName);
}
