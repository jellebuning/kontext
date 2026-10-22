using Microsoft.Extensions.DependencyInjection;

namespace Kontext.TerminalHost;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKontextTerminalHost(this IServiceCollection services)
    {
        services.AddSingleton<TerminalZoom>();
        return services;
    }
}
