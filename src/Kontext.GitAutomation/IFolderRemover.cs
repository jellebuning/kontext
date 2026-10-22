namespace Kontext.GitAutomation;

internal interface IFolderRemover
{
    bool Exists(string path);

    void Delete(string path);
}
