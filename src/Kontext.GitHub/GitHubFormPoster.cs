using System.Net.Http.Headers;

namespace Kontext.GitHub;

internal sealed class GitHubFormPoster(HttpClient http)
{
    public async Task<string> PostAsync(string url, IReadOnlyDictionary<string, string> form, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Kontext", "1.0"));

            using var response = await http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode && !body.TrimStart().StartsWith('{'))
            {
                throw new GitHubException($"GitHub returned an error (HTTP {(int)response.StatusCode}) while signing in.");
            }

            return body;
        }
        catch (HttpRequestException ex)
        {
            throw new GitHubException("Could not reach GitHub. Check your internet connection.", ex);
        }
    }
}
