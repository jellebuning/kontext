using Kontext.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Kontext.GitHub;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKontextGitHub(this IServiceCollection services)
    {
        services.AddSingleton<IGitHubService, GitHubService>();
        return services;
    }
}
