namespace Kontext.Contracts;

public interface IGitHubTokenStore
{
    string? Load();

    void Save(string token);

    void Clear();
}
