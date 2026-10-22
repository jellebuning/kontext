using Kontext.Contracts;
using Windows.Security.Credentials;

namespace Kontext.Services;

internal sealed class CredentialLockerTokenStore : IGitHubTokenStore
{
    private const string Resource = "Kontext/GitHub";
    private const string UserName = "github-oauth-token";
    private const string LegacyUserName = "personal-access-token";
    private const int ElementNotFound = unchecked((int)0x80070490);

    private readonly PasswordVault _vault = new();

    public string? Load()
    {
        RemoveLegacy();

        var credential = Find(UserName);
        credential?.RetrievePassword();
        return credential?.Password;
    }

    public void Save(string token) => _vault.Add(new PasswordCredential(Resource, UserName, token));

    public void Clear()
    {
        var credential = Find(UserName);
        if (credential is not null)
        {
            _vault.Remove(credential);
        }
    }

    private void RemoveLegacy()
    {
        var legacy = Find(LegacyUserName);
        if (legacy is not null)
        {
            _vault.Remove(legacy);
        }
    }

    private PasswordCredential? Find(string userName)
    {
        try
        {
            return _vault.Retrieve(Resource, userName);
        }
        catch (Exception ex) when (ex.HResult == ElementNotFound)
        {
            return null;
        }
    }
}
