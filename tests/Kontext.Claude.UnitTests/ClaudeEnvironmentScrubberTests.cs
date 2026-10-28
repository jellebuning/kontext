namespace Kontext.Claude.UnitTests;

[TestFixture]
public class ClaudeEnvironmentScrubberTests
{
    private sealed class FakeEnvironment(params string[] initiallySet) : IEnvironmentVariables
    {
        public HashSet<string> Set { get; } = [.. initiallySet];

        public bool IsSet(string name) => Set.Contains(name);

        public void Remove(string name) => Set.Remove(name);
    }

    private static string[] Names(params string[] names) => names;

    [Test]
    public void RemovesEveryParentSessionVariable()
    {
        var environment = new FakeEnvironment([.. ClaudeEnvironmentScrubber.VariableNames]);

        var removed = new ClaudeEnvironmentScrubber(environment).Scrub();

        Assert.That(environment.Set, Is.Empty);
        Assert.That(removed, Is.EquivalentTo(ClaudeEnvironmentScrubber.VariableNames));
    }

    [Test]
    public void LeavesUserConfigurationAlone()
    {
        var environment = new FakeEnvironment("CLAUDE_CONFIG_DIR", "ANTHROPIC_API_KEY", "CLAUDE_CODE_USE_BEDROCK", "PATH", "CLAUDECODE");

        new ClaudeEnvironmentScrubber(environment).Scrub();

        Assert.That(environment.Set, Is.EquivalentTo(Names("CLAUDE_CONFIG_DIR", "ANTHROPIC_API_KEY", "CLAUDE_CODE_USE_BEDROCK", "PATH")));
    }

    [Test]
    public void DropsNoColorWhenLaunchedFromAClaudeSessionBecauseThatIsHowClaudeRunsItsTools()
    {
        var environment = new FakeEnvironment("CLAUDECODE", "NO_COLOR", "PATH");

        var removed = new ClaudeEnvironmentScrubber(environment).Scrub();

        Assert.That(environment.Set, Is.EquivalentTo(Names("PATH")));
        Assert.That(removed, Is.EquivalentTo(Names("CLAUDECODE", "NO_COLOR")));
    }

    [Test]
    public void KeepsANoColorTheUserSetOnPurpose()
    {
        var environment = new FakeEnvironment("NO_COLOR", "PATH");

        var removed = new ClaudeEnvironmentScrubber(environment).Scrub();

        Assert.That(environment.Set, Is.EquivalentTo(Names("NO_COLOR", "PATH")));
        Assert.That(removed, Is.Empty);
    }

    [Test]
    public void DoesNotMentionNoColorWhenItIsNotSetEvenIfLaunchedFromAClaudeSession()
    {
        var removed = new ClaudeEnvironmentScrubber(new FakeEnvironment("CLAUDECODE")).Scrub();

        Assert.That(removed, Is.EquivalentTo(Names("CLAUDECODE")));
    }

    [Test]
    public void DoesNothingWhenNoneAreSet()
    {
        var environment = new FakeEnvironment("PATH");

        var removed = new ClaudeEnvironmentScrubber(environment).Scrub();

        Assert.That(removed, Is.Empty);
        Assert.That(environment.Set, Is.EquivalentTo(Names("PATH")));
    }

    [Test]
    public void ReportsOnlyTheVariablesItActuallyRemoved()
    {
        var environment = new FakeEnvironment("CLAUDECODE", "CLAUDE_PID");

        var removed = new ClaudeEnvironmentScrubber(environment).Scrub();

        Assert.That(removed, Is.EquivalentTo(Names("CLAUDECODE", "CLAUDE_PID")));
    }

    [Test]
    public void ScrubsTheRealProcessEnvironment()
    {
        const string name = "CLAUDE_CODE_SESSION_ATTENDED";
        var original = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, "1");

            new ClaudeEnvironmentScrubber(new ProcessEnvironmentVariables()).Scrub();

            Assert.That(Environment.GetEnvironmentVariable(name), Is.Null);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, original);
        }
    }
}
