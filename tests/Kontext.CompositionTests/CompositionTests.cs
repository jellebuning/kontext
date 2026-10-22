using Kontext.Claude;
using Kontext.Contracts;
using Kontext.ExternalTools;
using Kontext.GitAutomation;
using Kontext.GitHub;
using Kontext.Kernel;
using Kontext.Presentation;
using Kontext.Sessions;
using Kontext.TerminalHost;
using Kontext.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace Kontext.CompositionTests;

[TestFixture]
public class CompositionTests
{
    private ServiceProvider _provider = null!;

    [OneTimeSetUp]
    public void BuildTheContainer()
    {
        var root = Path.Combine(Path.GetTempPath(), "kontext-composition");

        var services = new ServiceCollection()
            .AddKontextGitHub()
            .AddKontextGitAutomation(new GitAutomationOptions(root, Path.Combine(root, "worktrees"), Path.Combine(root, "repos")))
            .AddKontextTerminalHost()
            .AddKontextClaude(new ClaudeOptions(Path.Combine(root, ".claude")))
            .AddKontextExternalTools()
            .AddKontextSessions(new SessionsOptions(root))
            .AddKontextPresentation();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IGitHubTokenStore>(new FakeTokenStore());
        services.AddSingleton<IUiDispatcher, ImmediateDispatcher>();
        services.AddSingleton<IConfirmation, FakeConfirmation>();
        services.AddSingleton<ITerminalSessionService, FakeTerminalService>();

        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [OneTimeTearDown]
    public void DisposeTheContainer() => _provider.Dispose();

    private static readonly Type[] Contracts =
    [
        typeof(IGitHubService),
        typeof(IWorkspaceProvisioner),
        typeof(IExistingWorkspaceFinder),
        typeof(IRepositoryCloneStore),
        typeof(ISessionStore),
        typeof(IIssueWorkTracker),
        typeof(IIssueSessionRegistry),
        typeof(IIssueSessionCoordinator),
        typeof(IClaudeLauncher),
        typeof(IWorkspaceOpener),
        typeof(ClaudeEnvironmentScrubber),
        typeof(TerminalZoom),
    ];

    private static readonly Type[] ViewModels =
    [
        typeof(ShellViewModel),
        typeof(WorkspaceViewModel),
        typeof(IssueSidebarViewModel),
        typeof(IssueDetailViewModel),
        typeof(AppLifetime),
    ];

    [TestCaseSource(nameof(Contracts))]
    public void EveryContractTheModulesOfferCanBeResolved(Type contract)
    {
        Assert.That(_provider.GetService(contract), Is.Not.Null, contract.Name);
    }

    [TestCaseSource(nameof(ViewModels))]
    public void EveryViewModelAndItsServicesCanBeResolved(Type type)
    {
        Assert.That(_provider.GetService(type), Is.Not.Null, type.Name);
    }

    [Test]
    public void TheTrackerAndTheRegistryAreSharedByEveryoneWhoAsksForThem()
    {
        var tracker = _provider.GetRequiredService<IIssueWorkTracker>();
        var again = _provider.GetRequiredService<IIssueWorkTracker>();
        var registry = _provider.GetRequiredService<IIssueSessionRegistry>();
        var registryAgain = _provider.GetRequiredService<IIssueSessionRegistry>();

        Assert.That(again, Is.SameAs(tracker));
        Assert.That(registryAgain, Is.SameAs(registry));
    }
}
