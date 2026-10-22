namespace Kontext.Domain;

public sealed record GitHubUser(string Login, string? Name, Uri ProfileUrl);
