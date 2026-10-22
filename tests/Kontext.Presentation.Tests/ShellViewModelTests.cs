using Kontext.Domain;
using Kontext.TestSupport;

namespace Kontext.Presentation.Tests;

[TestFixture]
public class ShellViewModelTests
{
    private FakeGitHubService _gitHub = null!;
    private FakeTokenStore _tokens = null!;
    private InMemoryIssueWorkTracker _tracker = null!;
    private FakeCoordinator _coordinator = null!;
    private SelectionAutoStart _autoStart = null!;
    private IssueSidebarViewModel _sidebar = null!;
    private IssueDetailViewModel _detail = null!;
    private ShellViewModel _shell = null!;

    [SetUp]
    public void CreateShell() => Build(savedToken: null);

    [TearDown]
    public void Dispose()
    {
        _detail.Dispose();
        _sidebar.Dispose();
        _autoStart.Dispose();
    }

    private void Build(string? savedToken)
    {
        _gitHub = new FakeGitHubService { Issues = [Issues.Create(4, "Add notifications", "acme/memento")] };
        _tokens = new FakeTokenStore(savedToken);
        _tracker = new InMemoryIssueWorkTracker();
        _coordinator = new FakeCoordinator();
        _autoStart = new SelectionAutoStart(_coordinator, _tracker, new InMemoryIssueSessionRegistry(), TimeProvider.System, AutoStartOptions.Default);
        _sidebar = new IssueSidebarViewModel(_gitHub, _tracker, new FakeExistingWorkspaceFinder(), new WorkspaceIssues(_gitHub), TimeProvider.System);
        _detail = new IssueDetailViewModel(_coordinator, new FakeSessionClearer(), new FakeConfirmation(), new FakeWorkspaceOpener(), new InMemoryIssueSessionRegistry(), _autoStart);
        _shell = new ShellViewModel(_gitHub, _tokens, new WorkspaceViewModel(_sidebar, _detail, _autoStart));
    }

    [Test]
    public void StartsInTheStartingStateSoTheSignInFormNeverFlashes()
    {
        Assert.That(_shell.IsStarting, Is.True);
        Assert.That(_shell.ShowSignIn, Is.False, "the token box must stay hidden until we know there is no saved token");
        Assert.That(_shell.IsSignedIn, Is.False);
    }

    [Test]
    public async Task ShowsTheSignInFormOnceItIsKnownThereIsNoSavedToken()
    {
        await _shell.InitializeCommand.ExecuteAsync(null);

        Assert.That(_shell.IsStarting, Is.False);
        Assert.That(_shell.ShowSignIn, Is.True);
        Assert.That(_gitHub.TokensTried, Is.Empty);
    }

    [Test]
    public async Task SignsInSilentlyWithASavedTokenAndNeverShowsTheSignInForm()
    {
        Build(savedToken: "ghp_saved");
        var shownSignIn = false;
        _shell.PropertyChanged += (_, _) => shownSignIn |= _shell.ShowSignIn;

        await _shell.InitializeCommand.ExecuteAsync(null);

        Assert.That(shownSignIn, Is.False, "ShowSignIn must not be true at any point while starting");
        Assert.That(_shell.IsStarting, Is.False);
        Assert.That(_shell.IsSignedIn, Is.True);
        Assert.That(_shell.ShowSignIn, Is.False);
        Assert.That(_gitHub.TokensTried, Is.EqualTo(Expect.Seq("ghp_saved")));
        Assert.That(_tokens.Saved, Is.EqualTo("ghp_saved"), "a valid saved token is kept");
    }

    [Test]
    public async Task LoadsTheIssuesRightAfterASilentSignIn()
    {
        Build(savedToken: "ghp_saved");

        await _shell.InitializeCommand.ExecuteAsync(null);

        await AsyncAssert.EventuallyAsync(() => _gitHub.Calls == 1, "the issue load");
        Assert.That(_shell.SignedInText, Does.Contain("octocat"));
    }

    [Test]
    public async Task ClearsARevokedSavedTokenAndAsksToSignInAgain()
    {
        Build(savedToken: "ghp_revoked");
        _gitHub.AuthenticationFailsWith = new AuthenticationFailedException("rejected");

        await _shell.InitializeCommand.ExecuteAsync(null);

        Assert.That(_tokens.Saved, Is.Null);
        Assert.That(_tokens.Clears, Is.EqualTo(1));
        Assert.That(_shell.ErrorMessage, Does.Contain("no longer valid"));
        Assert.That(_shell.IsStarting, Is.False);
        Assert.That(_shell.ShowSignIn, Is.True);
        Assert.That(_shell.IsSignedIn, Is.False);
    }

    [Test]
    public async Task KeepsTheSavedTokenWhenGitHubIsUnreachableAndShowsWhy()
    {
        Build(savedToken: "ghp_saved");
        _gitHub.AuthenticationFailsWith = new TestKontextException("Could not reach GitHub.");

        await _shell.InitializeCommand.ExecuteAsync(null);

        Assert.That(_tokens.Saved, Is.EqualTo("ghp_saved"), "being offline must not throw away a good token");
        Assert.That(_shell.ErrorMessage, Is.EqualTo("Could not reach GitHub."));
        Assert.That(_shell.IsStarting, Is.False);
        Assert.That(_shell.ShowSignIn, Is.True);
    }

    [Test]
    public void EndsTheStartingStateEvenWhenReadingTheSavedTokenFails()
    {
        _tokens.LoadFailsWith = new InvalidOperationException("credential locker unavailable");

        Assert.That(async () => await _shell.InitializeCommand.ExecuteAsync(null), Throws.InvalidOperationException);
        Assert.That(_shell.IsStarting, Is.False, "never leave the user on a loading screen forever");
    }

    [TestCase("")]
    [TestCase("   ")]
    public void CannotSignInWithoutAToken(string token)
    {
        _shell.Token = token;

        Assert.That(_shell.SignInCommand.CanExecute(null), Is.False);
    }

    [Test]
    public async Task SignsInWithTheTrimmedTokenSavesItAndClearsTheField()
    {
        _shell.Token = "  ghp_typed  ";
        Assert.That(_shell.SignInCommand.CanExecute(null), Is.True);

        await _shell.SignInCommand.ExecuteAsync(null);

        Assert.That(_gitHub.TokensTried, Is.EqualTo(Expect.Seq("ghp_typed")));
        Assert.That(_tokens.Saved, Is.EqualTo("ghp_typed"));
        Assert.That(_shell.Token, Is.Empty, "the token is not kept in the view model after use");
        Assert.That(_shell.IsSignedIn, Is.True);
    }

    [Test]
    public async Task ShowsTheErrorAndSavesNothingWhenTheTokenIsRejected()
    {
        _gitHub.AuthenticationFailsWith = new AuthenticationFailedException("GitHub rejected this token.");
        _shell.Token = "ghp_wrong";

        await _shell.SignInCommand.ExecuteAsync(null);

        Assert.That(_shell.ErrorMessage, Is.EqualTo("GitHub rejected this token."));
        Assert.That(_tokens.Saved, Is.Null);
        Assert.That(_shell.IsSignedIn, Is.False);
        Assert.That(_shell.IsBusy, Is.False);
    }

    [Test]
    public async Task SigningOutForgetsTheTokenClearsTheSidebarAndShowsTheSignInFormAgain()
    {
        Build(savedToken: "ghp_saved");
        await _shell.InitializeCommand.ExecuteAsync(null);
        await AsyncAssert.EventuallyAsync(() => _sidebar.Groups.Count == 1, "the sidebar to load");

        _shell.SignOutCommand.Execute(null);

        Assert.That(_gitHub.SignOuts, Is.EqualTo(1));
        Assert.That(_tokens.Saved, Is.Null);
        Assert.That(_sidebar.Groups, Is.Empty);
        Assert.That(_shell.IsSignedIn, Is.False);
        Assert.That(_shell.ShowSignIn, Is.True, "startup is over, so the form is shown");
    }

    [Test]
    public async Task SigningOutClosesTheSettingsSoTheNextSignInStartsOnTheIssues()
    {
        Build(savedToken: "ghp_saved");
        await _shell.InitializeCommand.ExecuteAsync(null);
        await AsyncAssert.EventuallyAsync(() => _sidebar.Groups.Count == 1, "the sidebar to load");
        _shell.Workspace.IsSettingsOpen = true;

        _shell.SignOutCommand.Execute(null);

        Assert.That(_shell.Workspace.IsSettingsOpen, Is.False);
        Assert.That(_shell.Workspace.IsDetailVisible, Is.True);
    }
}
