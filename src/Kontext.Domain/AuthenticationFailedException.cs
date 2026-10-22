using Kontext.Kernel;

namespace Kontext.Domain;

public class AuthenticationFailedException : KontextException
{
    public AuthenticationFailedException(string message)
        : base(message)
    {
    }

    public AuthenticationFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
