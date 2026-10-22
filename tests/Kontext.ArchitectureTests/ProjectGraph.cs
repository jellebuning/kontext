using System.Xml.Linq;

namespace Kontext.ArchitectureTests;

internal static class ProjectGraph
{
    public const string Ui = "Kontext";
    public const string Kernel = "Kontext.Kernel";
    public const string Domain = "Kontext.Domain";
    public const string Contracts = "Kontext.Contracts";

    public static readonly string[] Shared = [Kernel, Domain, Contracts];

    public static readonly string[] Modules =
    [
        "Kontext.GitHub",
        "Kontext.GitAutomation",
        "Kontext.TerminalHost",
        "Kontext.Claude",
        "Kontext.ExternalTools",
        "Kontext.Sessions",
        "Kontext.Presentation",
    ];

    public static IEnumerable<string> NonUi => [.. Shared, .. Modules];

    public static string SourceDirectory => Path.Combine(FindRepositoryRoot(), "src");

    public static IReadOnlyDictionary<string, IReadOnlySet<string>> Load()
    {
        var srcDirectory = SourceDirectory;

        return Directory.EnumerateFiles(srcDirectory, "*.csproj", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetFileNameWithoutExtension(path),
                path => (IReadOnlySet<string>)XDocument.Load(path)
                    .Descendants("ProjectReference")
                    .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/')))
                    .ToHashSet());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Kontext.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Kontext.slnx not found above the test output directory.");
    }
}
