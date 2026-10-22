using System.Diagnostics;

namespace Kontext.ExternalTools;

internal sealed class VswhereVisualStudioLocator : IVisualStudioLocator
{
    private static readonly string[] Arguments = ["-latest", "-prerelease", "-property", "productPath"];

    private static readonly string VswherePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        "Microsoft Visual Studio",
        "Installer",
        "vswhere.exe");

    public string? FindDevenv()
    {
        if (!File.Exists(VswherePath))
        {
            return null;
        }

        var startInfo = new ProcessStartInfo(VswherePath)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return null;
        }

        var path = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();

        return path.Length > 0 && File.Exists(path) ? path : null;
    }
}
