namespace Kontext.ExternalTools;

internal interface IFolderFiles
{
    bool Exists(string folder);

    IReadOnlyList<string> Find(string folder, string pattern);
}
