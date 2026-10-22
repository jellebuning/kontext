namespace Kontext.GitAutomation;

internal sealed class FolderRemover : IFolderRemover
{
    public bool Exists(string path) => Directory.Exists(path);

    public void Delete(string path)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new GitAutomationException(
                $"'{path}' could not be deleted because something is still using it ({ex.Message}). Close the programs running from this folder, such as a terminal or an app Claude started, and clear the session again.",
                innerException: ex);
        }
    }
}
