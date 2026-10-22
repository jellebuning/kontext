using System.Text.Json;
using Kontext.Contracts;
using Kontext.Kernel;

namespace Kontext.GitAutomation;

internal sealed class JsonRepositoryCloneStore(GitAutomationOptions options) : IRepositoryCloneStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Lock _gate = new();
    private readonly string _filePath = Path.Combine(options.StateDirectory, "clones.json");

    public string? Find(RepositoryRef repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        lock (_gate)
        {
            return Load().GetValueOrDefault(repository.FullName);
        }
    }

    public IReadOnlyList<RepositoryRef> List()
    {
        lock (_gate)
        {
            return Load().Keys
                .Select(name => name.Split('/'))
                .Where(parts => parts is [{ Length: > 0 }, { Length: > 0 }])
                .Select(parts => new RepositoryRef(parts[0], parts[1]))
                .ToList();
        }
    }

    public void Save(RepositoryRef repository, string localPath)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);

        lock (_gate)
        {
            var clones = Load();
            clones[repository.FullName] = localPath;

            Directory.CreateDirectory(options.StateDirectory);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(clones, JsonOptions));
        }
    }

    private Dictionary<string, string> Load()
    {
        var empty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(_filePath))
        {
            return empty;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_filePath));
            return stored is null ? empty : new Dictionary<string, string>(stored, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return empty;
        }
    }
}
