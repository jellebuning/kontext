using Kontext.Domain;
using Kontext.TestSupport;

namespace Kontext.Presentation.UnitTests;

[TestFixture]
public class ShellViewModelTests : ShellViewModelFixture
{
    [Test]
    public void StartsInTheStartingStateSoTheSignInFormNeverFlashes()
    {
        Assert.That(Shell.IsStarting, Is.True);
        Assert.That(Shell.ShowSignIn, Is.False, "the token box must stay hidden until we know there is no saved token");
        Assert.That(Shell.IsSignedIn, Is.False);
    }

    [Test]
    public async Task ShowsTheSignInFormOnceItIsKnownThereIsNoSavedToken()
    {
        await Shell.InitializeCommand.ExecuteAsync(null);

        Assert.That(Shell.IsStarting, Is.False);
        Assert.That(Shell.ShowSignIn, Is.True);
        Assert.That(GitHub.TokensTried, Is.Empty);
    }

    [Test]
    public async Task SignsInSilentlyWithASavedTokenAndNeverShowsTheSignInForm()
    {
        Build(savedToken: "ghp_saved");
        var shownSignIn = false;
        Shell.PropertyChanged += (_, _) => shownSignIn |= Shell.ShowSignIn;

        await Shell.InitializeCommand.ExecuteAsync(null);

        Assert.That(shownSignIn, Is.False, "ShowSignIn must not be true at any point while starting");
        Assert.That(Shell.IsStarting, Is.False);
        Assert.That(Shell.IsSignedIn, Is.True);
        Assert.That(Shell.ShowSignIn, Is.False);
        Assert.That(GitHub.TokensTried, Is.EqualTo(Expect.Seq("ghp_saved")));
        Assert.That(Tokens.Saved, Is.EqualTo("ghp_saved"), "a valid saved token is kept");
    }

    [Test]
    public async Task LoadsTheIssuesRightAfterASilentSignIn()
    {
        Build(savedToken: "ghp_saved");

        await Shell.InitializeCommand.ExecuteAsync(null);

        await AsyncAssert.EventuallyAsync(() => GitHub.Calls == 1, "the issue load");
        Assert.That(Shell.SignedInText, Does.Contain("octocat"));
    }

    [Test]
    public async Task ClearsARevokedSavedTokenAndAsksToSignInAgain()
    {
        Build(savedToken: "ghp_revoked");
        GitHub.AuthenticationFailsWith = new AuthenticationFailedException("rejected");

        await Shell.InitializeCommand.ExecuteAsync(null);

        Assert.That(Tokens.Saved, Is.Null);
        Assert.That(Tokens.Clears, Is.EqualTo(1));
        Assert.That(Shell.ErrorMessage, Does.Contain("no longer valid"));
        Assert.That(Shell.IsStarting, Is.False);
        Assert.That(Shell.ShowSignIn, Is.True);
        Assert.That(Shell.IsSignedIn, Is.False);
    }

    [Test]
    public async Task KeepsTheSavedTokenWhenGitHubIsUnreachableAndShowsWhy()
    {
        Build(savedToken: "ghp_saved");
        GitHub.AuthenticationFailsWith = new TestKontextException("Could not reach GitHub.");

        await Shell.InitializeCommand.ExecuteAsync(null);

        Assert.That(Tokens.Saved, Is.EqualTo("ghp_saved"), "being offline must not throw away a good token");
        Assert.That(Shell.ErrorMessage, Is.EqualTo("Could not reach GitHub."));
        Assert.That(Shell.IsStarting, Is.False);
        Assert.That(Shell.ShowSignIn, Is.True);
    }

    [Test]
    public void EndsTheStartingStateEvenWhenReadingTheSavedTokenFails()
    {
        Tokens.LoadFailsWith = new InvalidOperationException("credential locker unavailable");

        Assert.That(async () => await Shell.InitializeCommand.ExecuteAsync(null), Throws.InvalidOperationException);
        Assert.That(Shell.IsStarting, Is.False, "never leave the user on a loading screen forever");
    }

    [Test]
    public async Task SigningOutForgetsTheTokenClearsTheSidebarAndShowsTheSignInFormAgain()
    {
        await SignInWithASavedTokenAsync();

        Shell.SignOutCommand.Execute(null);

        Assert.That(GitHub.SignOuts, Is.EqualTo(1));
        Assert.That(Tokens.Saved, Is.Null);
        Assert.That(Sidebar.Groups, Is.Empty);
        Assert.That(Shell.IsSignedIn, Is.False);
        Assert.That(Shell.ShowSignIn, Is.True, "startup is over, so the form is shown");
    }

    [Test]
    public async Task SigningOutClosesTheSettingsSoTheNextSignInStartsOnTheIssues()
    {
        await SignInWithASavedTokenAsync();
        Shell.Workspace.IsSettingsOpen = true;

        Shell.SignOutCommand.Execute(null);

        Assert.That(Shell.Workspace.IsSettingsOpen, Is.False);
        Assert.That(Shell.Workspace.IsDetailVisible, Is.True);
    }

    private async Task SignInWithASavedTokenAsync()
    {
        Build(savedToken: "ghp_saved");
        await Shell.InitializeCommand.ExecuteAsync(null);
        await AsyncAssert.EventuallyAsync(() => Sidebar.Groups.Count == 1, "the sidebar to load");
    }
}
