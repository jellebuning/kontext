using Kontext.Claude;
using Kontext.Contracts;
using Kontext.ExternalTools;
using Kontext.GitAutomation;
using Kontext.GitHub;
using Kontext.Kernel;
using Kontext.Presentation;
using Kontext.Services;
using Kontext.Sessions;
using Kontext.Terminal;
using Kontext.TerminalHost;
using Kontext.Tray;
using Kontext.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;

namespace Kontext;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKontext(this IServiceCollection services)
    {
        var overridden = Environment.GetEnvironmentVariable("KONTEXT_DATA_DIR");
        var dataRoot = string.IsNullOrWhiteSpace(overridden)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kontext")
            : overridden;

        services
            .AddKontextGitHub(new GitHubOptions(GitHubClientId()))
            .AddKontextGitAutomation(new GitAutomationOptions(
                StateDirectory: dataRoot,
                WorktreesRoot: Path.Combine(dataRoot, "worktrees"),
                ClonesRoot: Path.Combine(dataRoot, "repos")))
            .AddKontextTerminalHost()
            .AddKontextClaude(new ClaudeOptions(ClaudeConfigDirectory()))
            .AddKontextExternalTools()
            .AddKontextSessions(new SessionsOptions(dataRoot))
            .AddKontextPresentation();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IWindowHandleProvider, WindowHandleProvider>();
        services.AddSingleton<XamlRootProvider>();
        services.AddSingleton<IConfirmation, ContentDialogConfirmation>();
        services.AddSingleton<IGitHubTokenStore, CredentialLockerTokenStore>();

        services.AddSingleton<IUiDispatcher>(_ => new DispatcherQueueDispatcher(DispatcherQueue.GetForCurrentThread()));

        services.AddSingleton<TerminalControlHost>();
        services.AddSingleton<ITerminalSessionService, EasyTerminalSessionService>();

        services.AddSingleton(new AppIconOptions(Path.Combine(AppContext.BaseDirectory, "Assets", "Kontext.ico")));
        services.AddSingleton<NotifyIcon>();
        services.AddSingleton<MenuTheme>();
        services.AddSingleton<TrayContextMenu>();
        services.AddSingleton<TrayHost>();

        services.AddTransient<ShellPage>();
        services.AddSingleton<MainWindow>();

        return services;
    }

    private static string? GitHubClientId()
    {
        var configured = Environment.GetEnvironmentVariable("KONTEXT_GITHUB_CLIENT_ID");
        return string.IsNullOrWhiteSpace(configured) ? null : configured.Trim();
    }

    private static string ClaudeConfigDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude")
            : configured;
    }
}
