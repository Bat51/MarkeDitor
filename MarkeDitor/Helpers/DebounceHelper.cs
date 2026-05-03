namespace MarkeDitor.Helpers;

public class DebounceHelper
{
    private readonly int _delayMs;
    private CancellationTokenSource? _cts;

    public DebounceHelper(int delayMs)
    {
        _delayMs = delayMs;
    }

    public async void Debounce(Action action)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            await Task.Delay(_delayMs, token);
            if (!token.IsCancellationRequested)
            {
                action();
            }
        }
        catch (TaskCanceledException)
        {
            // Expected when debounce is triggered again
        }
    }
}
