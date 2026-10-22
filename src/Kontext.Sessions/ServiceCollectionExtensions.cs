using Kontext.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Kontext.Sessions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKontextSessions(this IServiceCollection services, SessionsOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddSingleton<ISessionStore, JsonSessionStore>();
        services.AddSingleton<IIssueSessionRegistry, IssueSessionRegistry>();
        services.AddSingleton<IIssueWorkTracker, IssueWorkTracker>();
        services.AddSingleton<IIssueSessionCoordinator, IssueSessionCoordinator>();
        services.AddSingleton<IIssueSessionClearer, IssueSessionClearer>();
        return services;
    }
}
