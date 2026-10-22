namespace Kontext.Kernel;

public interface IConfirmation
{
    Task<bool> ConfirmAsync(string title, string message, string confirmText);
}
