namespace Kontext.Kernel;

public interface IUiDispatcher
{
    bool HasThreadAccess { get; }

    Task<T> RunAsync<T>(Func<T> work);

    Task RunAsync(Action action);
}
