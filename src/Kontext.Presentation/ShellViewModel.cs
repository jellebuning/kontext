using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Presentation;

public sealed partial class ShellViewModel(IGitHubService gitHub, IGitHubDeviceLogin deviceLogin, IGitHubTokenStore tokenStore, WorkspaceViewModel workspace) : ObservableObject
{
    public WorkspaceViewModel Workspace { get; } = workspace;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedIn), nameof(IsSignedOut), nameof(ShowSignIn), nameof(SignedInText))]
    public partial GitHubUser? User { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSignIn))]
    public partial bool IsStarting { get; set; } = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDeviceCode))]
    public partial string? UserCode { get; set; }

    [ObservableProperty]
    public partial Uri? VerificationUrl { get; set; }

    public bool HasDeviceCode => !string.IsNullOrEmpty(UserCode);

    public bool IsSignedIn => User is not null;

    public bool IsSignedOut => User is null;

    public bool ShowSignIn => IsSignedOut && !IsStarting;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public string SignedInText => User is null ? string.Empty : $"Signed in as {User.Name ?? User.Login} (@{User.Login})";

    [RelayCommand]
    private async Task InitializeAsync()
    {
        try
        {
            var savedToken = tokenStore.Load();
            if (savedToken is null)
            {
                return;
            }

            await SignInWithSavedTokenAsync(savedToken);
        }
        finally
        {
            IsStarting = false;
        }
    }

    private async Task SignInWithSavedTokenAsync(string savedToken)
    {
        IsBusy = true;
        try
        {
            User = await gitHub.AuthenticateAsync(savedToken);
            Workspace.Sidebar.RefreshCommand.Execute(null);
        }
        catch (AuthenticationFailedException)
        {
            tokenStore.Clear();
            ErrorMessage = "Your saved GitHub sign-in is no longer valid. Please sign in again.";
        }
        catch (KontextException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(CanSignIn))]
    private async Task SignInAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var challenge = await deviceLogin.StartAsync(cancellationToken);
            UserCode = challenge.UserCode;
            VerificationUrl = challenge.VerificationUri;

            var token = await deviceLogin.WaitForAccessTokenAsync(challenge, cancellationToken);
            User = await gitHub.AuthenticateAsync(token, cancellationToken);
            tokenStore.Save(token);
            Workspace.Sidebar.RefreshCommand.Execute(null);
        }
        catch (OperationCanceledException)
        {
        }
        catch (KontextException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            UserCode = null;
            VerificationUrl = null;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SignOut()
    {
        SignInCancelCommand.Execute(null);
        UserCode = null;
        VerificationUrl = null;
        gitHub.SignOut();
        tokenStore.Clear();
        Workspace.Sidebar.Clear();
        Workspace.IsSettingsOpen = false;
        ErrorMessage = null;
        User = null;
    }

    private bool CanSignIn() => !IsBusy;
}
