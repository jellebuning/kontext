using Microsoft.Extensions.DependencyInjection;

namespace Kontext.Presentation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKontextPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<AppLifetime>();
        services.AddSingleton(AutoStartOptions.Default);
        services.AddSingleton<IWorkspaceIssues, WorkspaceIssues>();
        services.AddSingleton<SelectionAutoStart>();

        services.AddTransient<IssueSidebarViewModel>();
        services.AddTransient<IssueDetailViewModel>();
        services.AddTransient<WorkspaceViewModel>();
        services.AddTransient<ShellViewModel>();

        return services;
    }
}
