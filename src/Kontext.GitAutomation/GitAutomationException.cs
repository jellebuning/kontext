using Kontext.Kernel;

namespace Kontext.GitAutomation;

public class GitAutomationException : KontextException
{
    public GitAutomationException(string message, int exitCode = -1, string? standardError = null, Exception? innerException = null)
        : base(message, innerException!)
    {
        ExitCode = exitCode;
        StandardError = standardError;
    }

    public int ExitCode { get; }

    public string? StandardError { get; }
}
