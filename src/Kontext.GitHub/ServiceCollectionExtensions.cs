using Kontext.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Kontext.GitHub;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKontextGitHub(this IServiceCollection services, GitHubOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton(new HttpClient());
        services.AddSingleton<GitHubFormPoster>();
        services.AddSingleton<IGitHubDeviceLogin, GitHubDeviceLogin>();
        services.AddSingleton<IGitHubService, GitHubService>();
        return services;
    }
}
