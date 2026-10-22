using System.ComponentModel;
using System.Diagnostics;
using Kontext.Domain;

namespace Kontext.ExternalTools;

internal sealed class ProcessStarter : IProcessStarter
{
    public void Start(ProcessLaunch launch)
    {
        ArgumentNullException.ThrowIfNull(launch);

        var startInfo = new ProcessStartInfo(launch.FileName) { UseShellExecute = launch.UseShellExecute };

        if (launch.WorkingDirectory is not null)
        {
            startInfo.WorkingDirectory = launch.WorkingDirectory;
        }

        foreach (var argument in launch.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            Process.Start(startInfo)?.Dispose();
        }
        catch (Win32Exception ex)
        {
            throw new ExternalToolException($"{launch.FileName} could not be started: {ex.Message}", ex);
        }
    }
}
