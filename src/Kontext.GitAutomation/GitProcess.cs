using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Kontext.GitAutomation;

internal sealed class GitProcess(IGitAuthentication authentication) : IGitProcess
{
    private const int DirectoryNotFoundExitCode = 128;

    public async Task<GitResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(workingDirectory))
        {
            return new GitResult(DirectoryNotFoundExitCode, string.Empty, $"Directory not found: {workingDirectory}");
        }

        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        foreach (var (name, value) in authentication.EnvironmentVariables())
        {
            startInfo.Environment[name] = value;
        }

        Process process;
        try
        {
            process = Process.Start(startInfo) ?? throw new GitAutomationException("git could not be started.");
        }
        catch (Win32Exception ex)
        {
            throw new GitAutomationException("git was not found. Install Git for Windows and make sure it is on your PATH.", innerException: ex);
        }

        using (process)
        {
            process.StandardInput.Close();
            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }

            return new GitResult(process.ExitCode, await standardOutput, await standardError);
        }
    }
}
