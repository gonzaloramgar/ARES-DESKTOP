using System.Windows.Threading;

namespace AresAssistant.Core;

public sealed class SchedulerService
{
    private readonly ScheduledTaskStore _store;
    private readonly Func<ScheduledTaskItem, Task> _runTask;
    private readonly DispatcherTimer _timer;

    public bool Enabled { get; set; } = true;

    public SchedulerService(ScheduledTaskStore store, Func<ScheduledTaskItem, Task> runTask)
    {
        _store = store;
        _runTask = runTask;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _timer.Tick += OnTick;
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private async void OnTick(object? sender, EventArgs e)
    {
        if (!Enabled) return;

        var now = DateTime.Now;
        var hhmm = now.ToString("HH:mm");

        foreach (var task in _store.GetAll().Where(t => t.Enabled && t.Time == hhmm))
        {
            // Run at most once per minute stamp.
            if (task.LastRunAt is { } last && last.ToString("yyyy-MM-dd HH:mm") == now.ToString("yyyy-MM-dd HH:mm"))
                continue;

            task.LastRunAt = now;
            _store.Upsert(task);

            try { await _runTask(task).ConfigureAwait(false); }
            catch { /* best effort scheduler */ }
        }
    }
}
