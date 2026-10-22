using Kontext.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Kontext.GitAutomation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKontextGitAutomation(this IServiceCollection services, GitAutomationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddSingleton<IGitAuthentication, GitHubTokenAuthentication>();
        services.AddSingleton<IGitProcess, GitProcess>();
        services.AddSingleton<IBranchNamer, BranchNamer>();
        services.AddSingleton<IWorktreePathProvider, WorktreePathProvider>();
        services.AddSingleton<IGitWorktreeService, GitWorktreeService>();
        services.AddSingleton<IGitRepositoryInspector, GitRepositoryInspector>();
        services.AddSingleton<IRepositoryCloneStore, JsonRepositoryCloneStore>();
        services.AddSingleton<IRepositoryCloner, RepositoryCloner>();
        services.AddSingleton<IRepositoryCloneResolver, RepositoryCloneResolver>();
        services.AddSingleton<IWorkspaceProvisioner, WorkspaceProvisioner>();
        services.AddSingleton<IExistingWorkspaceFinder, ExistingWorkspaceFinder>();
        services.AddSingleton<IFolderRemover, FolderRemover>();
        services.AddSingleton<IWorkspaceRemover, WorkspaceRemover>();

        return services;
    }
}
