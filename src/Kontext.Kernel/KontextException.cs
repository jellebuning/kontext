namespace Kontext.Kernel;

public class KontextException : Exception
{
    public KontextException()
    {
    }

    public KontextException(string message)
        : base(message)
    {
    }

    public KontextException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
