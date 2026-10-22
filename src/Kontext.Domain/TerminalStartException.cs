using Kontext.Kernel;

namespace Kontext.Domain;

public class TerminalStartException : KontextException
{
    public TerminalStartException(string message)
        : base(message)
    {
    }

    public TerminalStartException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
