using Kontext.Domain;
using Kontext.TestSupport;

namespace Kontext.Presentation.Tests;

[TestFixture]
public class ShellViewModelSignInTests : ShellViewModelFixture
{
    private async Task<Task> StartSignInAndAwaitApprovalAsync()
    {
        DeviceLogin.Deferred = true;
        var signIn = Shell.SignInCommand.ExecuteAsync(null);
        await AsyncAssert.EventuallyAsync(() => DeviceLogin.Waited == 1, "the wait for approval to start");
        return signIn;
    }

    private void AssertSignedOutAndIdle()
    {
        Assert.That(Tokens.Saved, Is.Null);
        Assert.That(Shell.IsSignedIn, Is.False);
        Assert.That(Shell.IsBusy, Is.False);
        Assert.That(Shell.HasDeviceCode, Is.False);
        Assert.That(Shell.VerificationUrl, Is.Null);
    }

    [Test]
    public async Task ShowsTheDeviceCodeAndVerificationUrlWhileWaitingForTheUserToApprove()
    {
        var signIn = await StartSignInAndAwaitApprovalAsync();

        Assert.That(Shell.UserCode, Is.EqualTo("WDJB-MJHT"));
        Assert.That(Shell.VerificationUrl, Is.EqualTo(new Uri("https://github.com/login/device")));
        Assert.That(Shell.HasDeviceCode, Is.True);
        Assert.That(Shell.IsBusy, Is.True);
        Assert.That(Shell.IsSignedIn, Is.False);
        Assert.That(Tokens.Saved, Is.Null);

        DeviceLogin.Grant();
        await signIn;
    }

    [Test]
    public async Task SignsInWithTheTokenFromTheDeviceFlowSavesItClearsTheDeviceCodeAndLoadsTheIssues()
    {
        var signIn = await StartSignInAndAwaitApprovalAsync();

        DeviceLogin.Grant("gho_granted");
        await signIn;

        Assert.That(GitHub.TokensTried, Is.EqualTo(Expect.Seq("gho_granted")));
        Assert.That(Tokens.Saved, Is.EqualTo("gho_granted"));
        Assert.That(Shell.IsSignedIn, Is.True);
        Assert.That(Shell.ErrorMessage, Is.Null);
        Assert.That(Shell.UserCode, Is.Null);
        Assert.That(Shell.VerificationUrl, Is.Null);
        Assert.That(Shell.HasDeviceCode, Is.False);
        Assert.That(Shell.IsBusy, Is.False);
        Assert.That(DeviceLogin.Started, Is.EqualTo(1));

        await AsyncAssert.EventuallyAsync(() => GitHub.Calls == 1, "the issue load");
        Assert.That(Shell.SignedInText, Does.Contain("octocat"));
    }

    [Test]
    public async Task OnlySavesTheTokenAfterGitHubHasAcceptedIt()
    {
        string? savedWhenAuthenticating = "not called";
        GitHub.OnAuthenticate = _ => savedWhenAuthenticating = Tokens.Saved;

        await Shell.SignInCommand.ExecuteAsync(null);

        Assert.That(savedWhenAuthenticating, Is.Null, "the token must not be stored before it has been validated");
        Assert.That(Tokens.Saved, Is.EqualTo("gho_fake"));
    }

    [Test]
    public async Task ShowsTheErrorAndSavesNothingWhenGitHubRejectsTheNewToken()
    {
        GitHub.AuthenticationFailsWith = new AuthenticationFailedException("GitHub rejected this token.");

        await Shell.SignInCommand.ExecuteAsync(null);

        Assert.That(Shell.ErrorMessage, Is.EqualTo("GitHub rejected this token."));
        AssertSignedOutAndIdle();
    }

    [Test]
    public async Task ShowsTheErrorWhenTheDeviceFlowCannotBeStartedAndSavesNothing()
    {
        DeviceLogin.StartFailsWith = new TestKontextException("Could not reach GitHub.");

        await Shell.SignInCommand.ExecuteAsync(null);

        Assert.That(Shell.ErrorMessage, Is.EqualTo("Could not reach GitHub."));
        Assert.That(GitHub.TokensTried, Is.Empty);
        Assert.That(DeviceLogin.Waited, Is.Zero, "there is nothing to wait for without a code");
        AssertSignedOutAndIdle();
    }

    [TestCase("The sign-in code expired. Please try again.")]
    [TestCase("Sign-in was cancelled on GitHub.")]
    public async Task ShowsTheErrorAndClearsTheCodeWhenWaitingForApprovalFails(string message)
    {
        DeviceLogin.WaitFailsWith = new AuthenticationFailedException(message);

        await Shell.SignInCommand.ExecuteAsync(null);

        Assert.That(Shell.ErrorMessage, Is.EqualTo(message));
        Assert.That(GitHub.TokensTried, Is.Empty);
        AssertSignedOutAndIdle();
    }

    [Test]
    public async Task ClearsAnEarlierErrorWhenSigningInAgain()
    {
        DeviceLogin.StartFailsWith = new TestKontextException("Could not reach GitHub.");
        await Shell.SignInCommand.ExecuteAsync(null);
        DeviceLogin.StartFailsWith = null;

        await Shell.SignInCommand.ExecuteAsync(null);

        Assert.That(Shell.ErrorMessage, Is.Null);
        Assert.That(Shell.IsSignedIn, Is.True);
    }

    [Test]
    public async Task CancellingTheSignInWhileWaitingEndsQuietly()
    {
        var signIn = await StartSignInAndAwaitApprovalAsync();

        Shell.SignInCancelCommand.Execute(null);
        await signIn;

        Assert.That(Shell.ErrorMessage, Is.Null);
        Assert.That(GitHub.TokensTried, Is.Empty);
        AssertSignedOutAndIdle();
    }

    [Test]
    public async Task CanSignInAgainAfterCancelling()
    {
        var first = await StartSignInAndAwaitApprovalAsync();
        Shell.SignInCancelCommand.Execute(null);
        await first;

        Assert.That(Shell.SignInCommand.CanExecute(null), Is.True);
        DeviceLogin.Deferred = false;
        await Shell.SignInCommand.ExecuteAsync(null);

        Assert.That(Shell.IsSignedIn, Is.True);
        Assert.That(Tokens.Saved, Is.EqualTo("gho_fake"));
    }

    [Test]
    public async Task CannotStartASecondSignInWhileOneIsInProgress()
    {
        Assert.That(Shell.SignInCommand.CanExecute(null), Is.True);

        var signIn = await StartSignInAndAwaitApprovalAsync();

        Assert.That(Shell.IsBusy, Is.True);
        Assert.That(Shell.SignInCommand.CanExecute(null), Is.False);

        DeviceLogin.Grant();
        await signIn;
        Assert.That(DeviceLogin.Started, Is.EqualTo(1));
    }

    [Test]
    public async Task SigningOutDuringASignInCancelsItAndClearsTheDeviceCode()
    {
        var signIn = await StartSignInAndAwaitApprovalAsync();

        Shell.SignOutCommand.Execute(null);

        Assert.That(Shell.HasDeviceCode, Is.False);
        Assert.That(Shell.UserCode, Is.Null);

        await signIn;
        Assert.That(Shell.ErrorMessage, Is.Null);
        Assert.That(GitHub.TokensTried, Is.Empty, "a cancelled sign-in must not complete after signing out");
        AssertSignedOutAndIdle();
    }

    [Test]
    public async Task TheSignInFormStaysHiddenWhileStartingEvenAfterASignInAttempt()
    {
        Assert.That(Shell.IsStarting, Is.True);

        DeviceLogin.StartFailsWith = new TestKontextException("Could not reach GitHub.");
        await Shell.SignInCommand.ExecuteAsync(null);

        Assert.That(Shell.IsStarting, Is.True);
        Assert.That(Shell.ShowSignIn, Is.False, "the form only shows once startup has finished");
    }
}
