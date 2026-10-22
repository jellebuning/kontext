using Kontext.Kernel;

namespace Kontext.Domain;

public class ExternalToolException : KontextException
{
    public ExternalToolException(string message)
        : base(message)
    {
    }

    public ExternalToolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
