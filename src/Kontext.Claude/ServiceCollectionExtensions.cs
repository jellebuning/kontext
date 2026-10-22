using Kontext.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Kontext.Claude;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKontextClaude(this IServiceCollection services, ClaudeOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddSingleton<ClaudeLaunchBuilder>();
        services.AddSingleton<IClaudeTranscripts, ClaudeTranscripts>();
        services.AddSingleton<IClaudeHookServer, LoopbackClaudeHookServer>();
        services.AddSingleton<ClaudeActivityReporter>();
        services.AddSingleton<IClaudeHooks, ClaudeHooks>();
        services.AddSingleton<IClaudeLauncher, ClaudeLauncher>();
        services.AddSingleton<IEnvironmentVariables, ProcessEnvironmentVariables>();
        services.AddSingleton<ClaudeEnvironmentScrubber>();
        return services;
    }
}
