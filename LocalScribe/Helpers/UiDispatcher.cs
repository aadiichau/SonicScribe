using Microsoft.UI.Dispatching;

namespace LocalScribe.Helpers;

public static class UiDispatcher
{
    private static DispatcherQueue? _queue;

    public static void Initialize(DispatcherQueue queue) => _queue = queue;

    public static void Invoke(Action action)
    {
        if (_queue is null)
        {
            action();
            return;
        }

        if (_queue.HasThreadAccess)
        {
            action();
            return;
        }

        _queue.TryEnqueue(() => action());
    }
}