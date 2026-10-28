using Kontext.Domain;

namespace Kontext.ExternalTools.UnitTests;

internal sealed class FakeProcessStarter : IProcessStarter
{
    public List<ProcessLaunch> Started { get; } = [];

    public HashSet<string> Missing { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void Start(ProcessLaunch launch)
    {
        if (Missing.Contains(launch.FileName))
        {
            throw new ExternalToolException($"{launch.FileName} could not be started.");
        }

        Started.Add(launch);
    }
}

internal sealed class FakeFolderFiles : IFolderFiles
{
    private readonly Dictionary<string, List<string>> _files = new(StringComparer.OrdinalIgnoreCase);

    public FakeFolderFiles WithFolder(string folder, params string[] files)
    {
        _files[folder] = [.. files];
        return this;
    }

    public bool Exists(string folder) => _files.ContainsKey(folder);

    public IReadOnlyList<string> Find(string folder, string pattern)
    {
        var suffix = pattern.TrimStart('*');
        return _files.TryGetValue(folder, out var files)
            ? files.Where(file => file.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)).ToList()
            : [];
    }
}
