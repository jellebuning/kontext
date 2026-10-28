using Kontext.Domain;

namespace Kontext.Claude.IntegrationTests;

[TestFixture]
public class ClaudeTranscriptsTests
{
    private string _root = null!;

    [SetUp]
    public void CreateScratchDirectory()
    {
        _root = Path.Combine(Path.GetTempPath(), "kontext-tests", Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void DeleteScratchDirectory()
    {
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.Delete(file);
        }

        foreach (var directory in Directory.EnumerateDirectories(_root, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
        {
            Directory.Delete(directory);
        }

        Directory.Delete(_root);
    }

    private ClaudeTranscripts CreateTranscripts() => new(new ClaudeOptions(_root));

    private void SaveTranscript(string projectFolder, TerminalSessionId id)
    {
        var folder = Path.Combine(_root, "projects", projectFolder);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, id.Value.ToString("D") + ".jsonl"), "{}");
    }

    [Test]
    public void FindsNothingWhenClaudeHasNeverRun()
    {
        Assert.That(CreateTranscripts().Exists(TerminalSessionId.New()), Is.False);
    }

    [Test]
    public void FindsAConversationInAnyProjectFolder()
    {
        var id = TerminalSessionId.New();
        SaveTranscript("C--Users-me-worktrees-4-add", id);

        Assert.That(CreateTranscripts().Exists(id), Is.True);
    }

    [Test]
    public void IgnoresConversationsOfOtherSessions()
    {
        SaveTranscript("C--Users-me-worktrees-4-add", TerminalSessionId.New());

        Assert.That(CreateTranscripts().Exists(TerminalSessionId.New()), Is.False);
    }
}
