using System.Globalization;
using Kontext.Kernel;
using Octokit;
using Octokit.Internal;

namespace Kontext.GitHub.UnitTests;

[TestFixture]
public class IssueMapperTests
{
    private static readonly string[] ExpectedLabels = ["bug", "help wanted"];

    private static Issue Parse(string json) => new SimpleJsonSerializer().Deserialize<Issue>(json);

    private static string IssueJson(bool withRepository = true, bool asPullRequest = false) => $$"""
        {
          "number": 12,
          "title": "Crash on startup",
          "body": "It crashes.",
          "state": "open",
          "html_url": "https://github.com/acme/app/issues/12",
          "created_at": "2025-03-01T10:00:00Z",
          "updated_at": "2025-03-05T12:30:00Z",
          "user": { "login": "octocat" },
          "labels": [ { "name": "bug" }, { "name": "help wanted" } ]
          {{(asPullRequest ? ", \"pull_request\": { \"html_url\": \"https://github.com/acme/app/pull/12\" }" : string.Empty)}}
          {{(withRepository ? ", \"repository\": { \"name\": \"app\", \"owner\": { \"login\": \"acme\" } }" : string.Empty)}}
        }
        """;

    [Test]
    public void MapsAnIssueToTheDomainModel()
    {
        var issue = IssueMapper.Map(Parse(IssueJson()), requestedRepository: null);

        Assert.That(issue, Is.Not.Null);
        Assert.That(issue!.Repository, Is.EqualTo(new RepositoryRef("acme", "app")));
        Assert.That(issue.Number, Is.EqualTo(12));
        Assert.That(issue.Title, Is.EqualTo("Crash on startup"));
        Assert.That(issue.Body, Is.EqualTo("It crashes."));
        Assert.That(issue.State, Is.EqualTo("open"));
        Assert.That(issue.Author, Is.EqualTo("octocat"));
        Assert.That(issue.Labels, Is.EqualTo(ExpectedLabels));
        Assert.That(issue.Url, Is.EqualTo(new Uri("https://github.com/acme/app/issues/12")));
        Assert.That(issue.UpdatedAt, Is.EqualTo(DateTimeOffset.Parse("2025-03-05T12:30:00Z", CultureInfo.InvariantCulture)));
    }

    [Test]
    public void SkipsPullRequests()
    {
        Assert.That(IssueMapper.Map(Parse(IssueJson(asPullRequest: true)), requestedRepository: null), Is.Null);
    }

    [Test]
    public void PrefersTheRepositoryTheIssueCarriesOverTheRequestedOne()
    {
        var requested = new RepositoryRef("acme", "requested");

        Assert.That(IssueMapper.Map(Parse(IssueJson()), requested)!.Repository, Is.EqualTo(new RepositoryRef("acme", "app")));
    }

    [Test]
    public void FallsBackToTheRequestedRepositoryWhenTheIssueCarriesNone()
    {
        var requested = new RepositoryRef("acme", "requested");

        Assert.That(IssueMapper.Map(Parse(IssueJson(withRepository: false)), requested)!.Repository, Is.EqualTo(requested));
    }

    [Test]
    public void FallsBackToTheIssueUrlWhenNothingElseIdentifiesTheRepository()
    {
        var issue = IssueMapper.Map(Parse(IssueJson(withRepository: false)), requestedRepository: null)!;

        Assert.That(issue.Repository, Is.EqualTo(new RepositoryRef("acme", "app")));
    }

    [Test]
    public void ToleratesMissingBodyAuthorAndLabels()
    {
        const string json = """
            {
              "number": 3, "title": "Bare", "state": "open",
              "html_url": "https://github.com/acme/app/issues/3",
              "created_at": "2025-03-01T10:00:00Z",
              "repository": { "name": "app", "owner": { "login": "acme" } }
            }
            """;

        var issue = IssueMapper.Map(Parse(json), requestedRepository: null)!;

        Assert.That(issue.Body, Is.Null);
        Assert.That(issue.Author, Is.Null);
        Assert.That(issue.Labels, Is.Empty);
        Assert.That(issue.UpdatedAt, Is.EqualTo(DateTimeOffset.Parse("2025-03-01T10:00:00Z", CultureInfo.InvariantCulture)));
    }
}
