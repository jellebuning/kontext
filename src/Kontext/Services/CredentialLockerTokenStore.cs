using Kontext.Contracts;
using Windows.Security.Credentials;

namespace Kontext.Services;

internal sealed class CredentialLockerTokenStore : IGitHubTokenStore
{
    private const string Resource = "Kontext/GitHub";
    private const string UserName = "personal-access-token";
    private const int ElementNotFound = unchecked((int)0x80070490);

    private readonly PasswordVault _vault = new();

    public string? Load()
    {
        var credential = Find();
        credential?.RetrievePassword();
        return credential?.Password;
    }

    public void Save(string token) => _vault.Add(new PasswordCredential(Resource, UserName, token));

    public void Clear()
    {
        var credential = Find();
        if (credential is not null)
        {
            _vault.Remove(credential);
        }
    }

    private PasswordCredential? Find()
    {
        try
        {
            return _vault.Retrieve(Resource, UserName);
        }
        catch (Exception ex) when (ex.HResult == ElementNotFound)
        {
            return null;
        }
    }
}
