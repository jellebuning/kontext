using Kontext.Domain;
using Kontext.TestSupport;

namespace Kontext.Presentation.Tests;

[TestFixture]
public class ShellViewModelTests
{
    private FakeGitHubService _gitHub = null!;
    private FakeGitHubDeviceLogin _deviceLogin = null!;
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
        _deviceLogin = new FakeGitHubDeviceLogin();
        _shell = new ShellViewModel(_gitHub, _deviceLogin, _tokens, new WorkspaceViewModel(_sidebar, _detail, _autoStart));
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

    [Test]
    public async Task ShowsTheDeviceCodeAndVerificationUrlWhileWaitingForTheUserToApprove()
    {
        _deviceLogin.Deferred = true;

        var signIn = _shell.SignInCommand.ExecuteAsync(null);
        await AsyncAssert.EventuallyAsync(() => _deviceLogin.Waited == 1, "the wait for approval to start");

        Assert.That(_shell.UserCode, Is.EqualTo("WDJB-MJHT"));
        Assert.That(_shell.VerificationUrl, Is.EqualTo(new Uri("https://github.com/login/device")));
        Assert.That(_shell.HasDeviceCode, Is.True);
        Assert.That(_shell.IsBusy, Is.True);
        Assert.That(_shell.IsSignedIn, Is.False);
        Assert.That(_tokens.Saved, Is.Null);

        _deviceLogin.Grant();
        await signIn;
    }

    [Test]
    public async Task SignsInWithTheTokenFromTheDeviceFlowSavesItAndClearsTheDeviceCode()
    {
        _deviceLogin.Deferred = true;
        var signIn = _shell.SignInCommand.ExecuteAsync(null);
        await AsyncAssert.EventuallyAsync(() => _deviceLogin.Waited == 1, "the wait for approval to start");

        _deviceLogin.Grant("gho_granted");
        await signIn;

        Assert.That(_gitHub.TokensTried, Is.EqualTo(Expect.Seq("gho_granted")));
        Assert.That(_tokens.Saved, Is.EqualTo("gho_granted"));
        Assert.That(_shell.IsSignedIn, Is.True);
        Assert.That(_shell.ErrorMessage, Is.Null);
        Assert.That(_shell.UserCode, Is.Null);
        Assert.That(_shell.VerificationUrl, Is.Null);
        Assert.That(_shell.HasDeviceCode, Is.False);
        Assert.That(_shell.IsBusy, Is.False);
        Assert.That(_deviceLogin.Started, Is.EqualTo(1));
    }

    [Test]
    public async Task LoadsTheIssuesRightAfterSigningInWithGitHub()
    {
        await _shell.SignInCommand.ExecuteAsync(null);

        await AsyncAssert.EventuallyAsync(() => _gitHub.Calls == 1, "the issue load");
        Assert.That(_shell.SignedInText, Does.Contain("octocat"));
    }

    [Test]
    public async Task OnlySavesTheTokenAfterGitHubHasAcceptedIt()
    {
        string? savedWhenAuthenticating = "not called";
        _gitHub.OnAuthenticate = _ => savedWhenAuthenticating = _tokens.Saved;

        await _shell.SignInCommand.ExecuteAsync(null);

        Assert.That(savedWhenAuthenticating, Is.Null, "the token must not be stored before it has been validated");
        Assert.That(_tokens.Saved, Is.EqualTo("gho_fake"));
    }

    [Test]
    public async Task ShowsTheErrorAndSavesNothingWhenGitHubRejectsTheNewToken()
    {
        _gitHub.AuthenticationFailsWith = new AuthenticationFailedException("GitHub rejected this token.");

        await _shell.SignInCommand.ExecuteAsync(null);

        Assert.That(_shell.ErrorMessage, Is.EqualTo("GitHub rejected this token."));
        Assert.That(_tokens.Saved, Is.Null);
        Assert.That(_shell.IsSignedIn, Is.False);
        Assert.That(_shell.IsBusy, Is.False);
        Assert.That(_shell.HasDeviceCode, Is.False);
    }

    [Test]
    public async Task ShowsTheErrorWhenTheDeviceFlowCannotBeStartedAndSavesNothing()
    {
        _deviceLogin.StartFailsWith = new TestKontextException("Could not reach GitHub.");

        await _shell.SignInCommand.ExecuteAsync(null);

        Assert.That(_shell.ErrorMessage, Is.EqualTo("Could not reach GitHub."));
        Assert.That(_tokens.Saved, Is.Null);
        Assert.That(_gitHub.TokensTried, Is.Empty);
        Assert.That(_deviceLogin.Waited, Is.Zero, "there is nothing to wait for without a code");
        Assert.That(_shell.IsSignedIn, Is.False);
        Assert.That(_shell.IsBusy, Is.False);
        Assert.That(_shell.HasDeviceCode, Is.False);
    }

    [Test]
    public async Task ShowsTheNotConfiguredMessageWhenNoClientIdIsAvailable()
    {
        const string message = "GitHub sign-in is not configured. Set the KONTEXT_GITHUB_CLIENT_ID environment variable to the client id of a GitHub OAuth App with device flow enabled, then restart Kontext.";
        _deviceLogin.StartFailsWith = new TestKontextException(message);

        await _shell.SignInCommand.ExecuteAsync(null);

        Assert.That(_shell.ErrorMessage, Is.EqualTo(message));
        Assert.That(_shell.HasError, Is.True);
        Assert.That(_shell.IsBusy, Is.False);
    }

    [TestCase("The sign-in code expired. Please try again.")]
    [TestCase("Sign-in was cancelled on GitHub.")]
    public async Task ShowsTheErrorAndClearsTheCodeWhenWaitingForApprovalFails(string message)
    {
        _deviceLogin.WaitFailsWith = new AuthenticationFailedException(message);

        await _shell.SignInCommand.ExecuteAsync(null);

        Assert.That(_shell.ErrorMessage, Is.EqualTo(message));
        Assert.That(_tokens.Saved, Is.Null);
        Assert.That(_gitHub.TokensTried, Is.Empty);
        Assert.That(_shell.IsSignedIn, Is.False);
        Assert.That(_shell.IsBusy, Is.False);
        Assert.That(_shell.UserCode, Is.Null);
        Assert.That(_shell.VerificationUrl, Is.Null);
        Assert.That(_shell.HasDeviceCode, Is.False);
    }

    [Test]
    public async Task ClearsAnEarlierErrorWhenSigningInAgain()
    {
        _deviceLogin.StartFailsWith = new TestKontextException("Could not reach GitHub.");
        await _shell.SignInCommand.ExecuteAsync(null);
        _deviceLogin.StartFailsWith = null;

        await _shell.SignInCommand.ExecuteAsync(null);

        Assert.That(_shell.ErrorMessage, Is.Null);
        Assert.That(_shell.IsSignedIn, Is.True);
    }

    [Test]
    public async Task CancellingTheSignInWhileWaitingEndsQuietly()
    {
        _deviceLogin.Deferred = true;
        var signIn = _shell.SignInCommand.ExecuteAsync(null);
        await AsyncAssert.EventuallyAsync(() => _deviceLogin.Waited == 1, "the wait for approval to start");

        _shell.SignInCancelCommand.Execute(null);
        await signIn;

        Assert.That(_shell.ErrorMessage, Is.Null);
        Assert.That(_shell.IsSignedIn, Is.False);
        Assert.That(_tokens.Saved, Is.Null);
        Assert.That(_gitHub.TokensTried, Is.Empty);
        Assert.That(_shell.IsBusy, Is.False);
        Assert.That(_shell.HasDeviceCode, Is.False);
        Assert.That(_shell.VerificationUrl, Is.Null);
    }

    [Test]
    public async Task CanSignInAgainAfterCancelling()
    {
        _deviceLogin.Deferred = true;
        var first = _shell.SignInCommand.ExecuteAsync(null);
        await AsyncAssert.EventuallyAsync(() => _deviceLogin.Waited == 1, "the wait for approval to start");
        _shell.SignInCancelCommand.Execute(null);
        await first;

        Assert.That(_shell.SignInCommand.CanExecute(null), Is.True);
        _deviceLogin.Deferred = false;
        await _shell.SignInCommand.ExecuteAsync(null);

        Assert.That(_shell.IsSignedIn, Is.True);
        Assert.That(_tokens.Saved, Is.EqualTo("gho_fake"));
    }

    [Test]
    public async Task CannotStartASecondSignInWhileOneIsInProgress()
    {
        Assert.That(_shell.SignInCommand.CanExecute(null), Is.True);
        _deviceLogin.Deferred = true;

        var signIn = _shell.SignInCommand.ExecuteAsync(null);
        await AsyncAssert.EventuallyAsync(() => _deviceLogin.Waited == 1, "the wait for approval to start");

        Assert.That(_shell.IsBusy, Is.True);
        Assert.That(_shell.SignInCommand.CanExecute(null), Is.False);

        _deviceLogin.Grant();
        await signIn;
        Assert.That(_deviceLogin.Started, Is.EqualTo(1));
    }

    [Test]
    public async Task SigningOutDuringASignInCancelsItAndClearsTheDeviceCode()
    {
        _deviceLogin.Deferred = true;
        var signIn = _shell.SignInCommand.ExecuteAsync(null);
        await AsyncAssert.EventuallyAsync(() => _deviceLogin.Waited == 1, "the wait for approval to start");

        _shell.SignOutCommand.Execute(null);

        Assert.That(_shell.UserCode, Is.Null);
        Assert.That(_shell.VerificationUrl, Is.Null);
        Assert.That(_shell.HasDeviceCode, Is.False);

        await signIn;
        Assert.That(_shell.IsSignedIn, Is.False);
        Assert.That(_shell.IsBusy, Is.False);
        Assert.That(_shell.ErrorMessage, Is.Null);
        Assert.That(_tokens.Saved, Is.Null);
        Assert.That(_gitHub.TokensTried, Is.Empty, "a cancelled sign-in must not complete after signing out");
    }

    [Test]
    public async Task TheSignInFormStaysHiddenWhileStartingEvenAfterASignInAttempt()
    {
        Assert.That(_shell.IsStarting, Is.True);

        _deviceLogin.StartFailsWith = new TestKontextException("Could not reach GitHub.");
        await _shell.SignInCommand.ExecuteAsync(null);

        Assert.That(_shell.IsStarting, Is.True);
        Assert.That(_shell.ShowSignIn, Is.False, "the form only shows once startup has finished");
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
