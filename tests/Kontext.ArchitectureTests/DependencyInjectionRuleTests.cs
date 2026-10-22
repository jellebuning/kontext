using System.Text.RegularExpressions;

namespace Kontext.ArchitectureTests;

[TestFixture]
public partial class DependencyInjectionRuleTests
{
    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(ProjectGraph.SourceDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static List<string> FilesMatching(Regex pattern, Func<string, bool>? exempt = null) =>
        SourceFiles()
            .Where(path => exempt?.Invoke(path) != true)
            .Where(path => pattern.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(ProjectGraph.SourceDirectory, path))
            .ToList();

    [Test]
    public void OnlyTheCompositionRootResolvesFromTheContainer()
    {
        var offenders = FilesMatching(ServiceLocatorCall(), exempt: path => path.EndsWith("App.xaml.cs", StringComparison.Ordinal));

        Assert.That(offenders, Is.Empty, "Resolve dependencies through constructors; only App.xaml.cs may call the container.");
    }

    [Test]
    public void NoCodeReadsTheSystemClockDirectly()
    {
        var offenders = FilesMatching(DirectClockRead());

        Assert.That(offenders, Is.Empty, "Inject TimeProvider instead of reading DateTime/DateTimeOffset Now/UtcNow.");
    }

    [Test]
    public void NoStaticServiceHoldersExist()
    {
        var offenders = FilesMatching(StaticServiceAccessor());

        Assert.That(offenders, Is.Empty, "Static GetService<T>-style accessors are a service locator.");
    }

    [GeneratedRegex(@"\b(?:GetRequiredService|GetService|GetServices|GetRequiredKeyedService)\s*[<(]")]
    private static partial Regex ServiceLocatorCall();

    [GeneratedRegex(@"\b(?:DateTime|DateTimeOffset)\.(?:Utc)?Now\b")]
    private static partial Regex DirectClockRead();

    [GeneratedRegex(@"\bstatic\b[^;{=]*\bGetService\b")]
    private static partial Regex StaticServiceAccessor();
}
