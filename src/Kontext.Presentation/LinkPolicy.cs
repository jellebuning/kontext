namespace Kontext.Presentation;

public static class LinkPolicy
{
    public static bool IsSafeToOpen(Uri? link)
        => link is { IsAbsoluteUri: true } && (link.Scheme == Uri.UriSchemeHttp || link.Scheme == Uri.UriSchemeHttps);
}
