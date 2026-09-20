using System.Windows.Threading;

namespace HesabdariAsan.Native.Services;

/// <summary>
/// Coalesces rapid UI events (typing/search/filter changes) into a single action.
/// This keeps SQLite queries off the hot path of every keystroke while preserving
/// immediate-feeling native UI feedback.
/// </summary>
public sealed class DebounceDispatcher
{
    private readonly DispatcherTimer _timer;
    private Action? _pending;

    public DebounceDispatcher(TimeSpan delay)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = delay
        };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            var action = _pending;
            _pending = null;
            action?.Invoke();
        };
    }

    public void Schedule(Action action)
    {
        _pending = action;
        _timer.Stop();
        _timer.Start();
    }

    public void Cancel()
    {
        _timer.Stop();
        _pending = null;
    }
}
