using Kontext.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Kontext.ExternalTools;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKontextExternalTools(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IProcessStarter, ProcessStarter>();
        services.AddSingleton<IFolderFiles, FolderFiles>();
        services.AddSingleton<IWorkspaceTool, ExplorerTool>();
        services.AddSingleton<IWorkspaceTool, TerminalTool>();
        services.AddSingleton<IWorkspaceTool, SolutionTool>();
        services.AddSingleton<IWorkspaceOpener, WorkspaceOpener>();
        return services;
    }
}
