namespace Kontext.ExternalTools;

internal sealed class FolderFiles : IFolderFiles
{
    public bool Exists(string folder) => Directory.Exists(folder);

    public IReadOnlyList<string> Find(string folder, string pattern)
        => Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, pattern, SearchOption.TopDirectoryOnly).Order(StringComparer.OrdinalIgnoreCase).ToList()
            : [];
}
