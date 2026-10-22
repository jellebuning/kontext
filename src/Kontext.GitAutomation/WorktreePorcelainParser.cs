using Kontext.Domain;

namespace Kontext.GitAutomation;

internal static class WorktreePorcelainParser
{
    private const string BranchPrefix = "refs/heads/";
    private const string DetachedName = "(detached)";

    public static IReadOnlyList<WorktreeInfo> Parse(string porcelainOutput)
    {
        var worktrees = new List<WorktreeInfo>();
        string? path = null;
        string? head = null;
        var branch = DetachedName;
        var prunable = false;

        void Flush()
        {
            if (path is not null && !prunable)
            {
                worktrees.Add(new WorktreeInfo(path, branch, head, IsMain: worktrees.Count == 0));
            }

            path = null;
            head = null;
            branch = DetachedName;
            prunable = false;
        }

        foreach (var line in porcelainOutput.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (line.Length == 0)
            {
                Flush();
            }
            else if (line.StartsWith("worktree ", StringComparison.Ordinal))
            {
                Flush();
                path = Path.GetFullPath(line["worktree ".Length..]);
            }
            else if (line.StartsWith("HEAD ", StringComparison.Ordinal))
            {
                head = line["HEAD ".Length..];
            }
            else if (line.StartsWith("prunable", StringComparison.Ordinal))
            {
                prunable = true;
            }
            else if (line.StartsWith("branch ", StringComparison.Ordinal))
            {
                var reference = line["branch ".Length..];
                branch = reference.StartsWith(BranchPrefix, StringComparison.Ordinal) ? reference[BranchPrefix.Length..] : reference;
            }
        }

        Flush();
        return worktrees;
    }
}
