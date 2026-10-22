using Kontext.Kernel;
using Microsoft.UI.Dispatching;

namespace Kontext.Services;

internal sealed class DispatcherQueueDispatcher(DispatcherQueue queue) : IUiDispatcher
{
    public bool HasThreadAccess => queue.HasThreadAccess;

    public Task<T> RunAsync<T>(Func<T> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (queue.HasThreadAccess)
        {
            return Task.FromResult(work());
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        var queued = queue.TryEnqueue(() =>
        {
            try
            {
                completion.SetResult(work());
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                completion.SetException(ex);
            }
        });

        if (!queued)
        {
            completion.SetException(new InvalidOperationException("The UI thread is no longer accepting work."));
        }

        return completion.Task;
    }

    public Task RunAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return RunAsync<object?>(() =>
        {
            action();
            return null;
        });
    }
}
