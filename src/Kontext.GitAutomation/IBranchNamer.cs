using Kontext.Domain;

namespace Kontext.GitAutomation;

public interface IBranchNamer
{
    string Name(GitHubIssue issue);

    int? IssueNumberOf(string branchName);
}
