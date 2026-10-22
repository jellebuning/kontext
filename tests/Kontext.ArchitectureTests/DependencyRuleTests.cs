namespace Kontext.ArchitectureTests;

[TestFixture]
public class DependencyRuleTests
{
    private IReadOnlyDictionary<string, IReadOnlySet<string>> _graph = null!;

    [OneTimeSetUp]
    public void LoadGraph() => _graph = ProjectGraph.Load();

    [Test]
    public void SolutionContainsTheExpectedProjects()
    {
        string[] expected = [ProjectGraph.Ui, .. ProjectGraph.Shared, .. ProjectGraph.Modules];

        Assert.That(_graph.Keys, Is.EquivalentTo(expected));
    }

    [Test]
    public void KernelHasNoProjectReferences()
    {
        Assert.That(_graph[ProjectGraph.Kernel], Is.Empty);
    }

    [Test]
    public void DomainReferencesOnlyTheKernel()
    {
        Assert.That(_graph[ProjectGraph.Domain], Is.EquivalentTo(new[] { ProjectGraph.Kernel }));
    }

    [Test]
    public void ContractsReferenceOnlyTheDomainAndTheKernel()
    {
        Assert.That(_graph[ProjectGraph.Contracts], Is.EquivalentTo(new[] { ProjectGraph.Domain, ProjectGraph.Kernel }));
    }

    [TestCaseSource(typeof(ProjectGraph), nameof(ProjectGraph.Modules))]
    public void ModuleReferencesOnlySharedProjects(string module)
    {
        Assert.That(_graph[module], Is.SubsetOf(ProjectGraph.Shared), $"{module} may use the shared projects, never another module.");
    }

    [Test]
    public void UiReferencesEverySharedProjectAndEveryModule()
    {
        string[] expected = [.. ProjectGraph.Shared, .. ProjectGraph.Modules];

        Assert.That(_graph[ProjectGraph.Ui], Is.EquivalentTo(expected));
    }

    [TestCaseSource(typeof(ProjectGraph), nameof(ProjectGraph.NonUi))]
    public void OnlyTheUiProjectUsesWinUi(string project)
    {
        var path = Path.Combine(ProjectGraph.SourceDirectory, project, project + ".csproj");
        var text = File.ReadAllText(path);

        Assert.That(text, Does.Not.Contain("UseWinUI").And.Not.Contain("windows10").And.Not.Contain("WindowsAppSDK"),
            $"{project} must stay a plain net10.0 project; WinUI code belongs in Kontext.");
    }
    [Test]
    public void NothingReferencesTheUi()
    {
        var referrers = _graph.Where(entry => entry.Value.Contains(ProjectGraph.Ui)).Select(entry => entry.Key);

        Assert.That(referrers, Is.Empty);
    }

    [Test]
    public void GraphHasNoCycles()
    {
        foreach (var project in _graph.Keys)
        {
            Assert.That(CanReach(project, project), Is.False, $"{project} is part of a reference cycle.");
        }
    }

    private bool CanReach(string from, string target)
    {
        var visited = new HashSet<string>();
        var pending = new Stack<string>(_graph[from]);

        while (pending.TryPop(out var current))
        {
            if (current == target)
            {
                return true;
            }

            if (visited.Add(current) && _graph.TryGetValue(current, out var next))
            {
                foreach (var dependency in next)
                {
                    pending.Push(dependency);
                }
            }
        }

        return false;
    }
}
