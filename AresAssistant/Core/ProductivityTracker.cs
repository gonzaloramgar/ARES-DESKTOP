using System.Diagnostics;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace AresAssistant.Core;

public sealed class ProductivityTracker
{
    private readonly string _path;
    private readonly object _lock = new();
    private readonly Dictionary<string, ProductivityDayRecord> _days = new(StringComparer.OrdinalIgnoreCase);

    private System.Threading.Timer? _timer;
    private DateTime _lastSampleUtc = DateTime.UtcNow;
    private DateTime _lastSaveUtc = DateTime.UtcNow;

    public ProductivityTracker(string path)
    {
        _path = path;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        Load();
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_timer != null) return;
            _lastSampleUtc = DateTime.UtcNow;
            _timer = new System.Threading.Timer(_ => Tick(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            Save();
        }
    }

    public List<ProductivityAppUsage> GetTopApps(int max = 6)
    {
        lock (_lock)
        {
            var today = GetTodayRecord(DateOnly.FromDateTime(DateTime.Now));
            return today.AppSeconds
                .OrderByDescending(kv => kv.Value)
                .Take(Math.Max(1, max))
                .Select(kv => new ProductivityAppUsage(kv.Key, kv.Value))
                .ToList();
        }
    }

    public string GetDailySummary()
    {
        lock (_lock)
        {
            var today = GetTodayRecord(DateOnly.FromDateTime(DateTime.Now));
            return today.AiSummary ?? string.Empty;
        }
    }

    public void SetDailySummary(string summary)
    {
        lock (_lock)
        {
            var today = GetTodayRecord(DateOnly.FromDateTime(DateTime.Now));
            today.AiSummary = summary;
            today.SummaryUpdatedUtc = DateTime.UtcNow;
            Save();
        }
    }

    public string BuildDailySnapshotText(int max = 8)
    {
        lock (_lock)
        {
            var todayKey = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
            var today = GetTodayRecord(DateOnly.Parse(todayKey));
            var total = Math.Max(1, today.AppSeconds.Values.Sum());

            var lines = today.AppSeconds
                .OrderByDescending(kv => kv.Value)
                .Take(Math.Max(1, max))
                .Select(kv =>
                {
                    var pct = (int)Math.Round(kv.Value * 100.0 / total);
                    var minutes = Math.Round(kv.Value / 60.0, 1);
                    return $"- {kv.Key}: {minutes} min ({pct}%)";
                });

            return $"Fecha: {todayKey}{Environment.NewLine}" + string.Join(Environment.NewLine, lines);
        }
    }

    private void Tick()
    {
        try
        {
            var nowUtc = DateTime.UtcNow;
            var elapsedSec = (int)Math.Clamp((nowUtc - _lastSampleUtc).TotalSeconds, 1, 15);
            _lastSampleUtc = nowUtc;

            var app = GetForegroundProcessName();
            if (string.IsNullOrWhiteSpace(app)) return;

            lock (_lock)
            {
                var today = GetTodayRecord(DateOnly.FromDateTime(DateTime.Now));
                if (!today.AppSeconds.TryAdd(app, elapsedSec))
                    today.AppSeconds[app] += elapsedSec;

                // Flush periodically to avoid excessive disk writes.
                if ((nowUtc - _lastSaveUtc) >= TimeSpan.FromSeconds(30))
                {
                    Save();
                    _lastSaveUtc = nowUtc;
                }
            }
        }
        catch
        {
            // Background tracker must never crash the app.
        }
    }

    private ProductivityDayRecord GetTodayRecord(DateOnly day)
    {
        var key = day.ToString("yyyy-MM-dd");
        if (_days.TryGetValue(key, out var existing)) return existing;

        var created = new ProductivityDayRecord { Date = key };
        _days[key] = created;
        return created;
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            Save();
            return;
        }

        try
        {
            var raw = File.ReadAllText(_path);
            var data = JsonConvert.DeserializeObject<List<ProductivityDayRecord>>(raw) ?? new List<ProductivityDayRecord>();
            _days.Clear();
            foreach (var day in data)
            {
                if (!string.IsNullOrWhiteSpace(day.Date))
                    _days[day.Date] = day;
            }
        }
        catch
        {
            _days.Clear();
            Save();
        }
    }

    private void Save()
    {
        var payload = _days.Values.OrderBy(v => v.Date).ToList();
        var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
        File.WriteAllText(_path, json);
    }

    private static string? GetForegroundProcessName()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;

        _ = GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0) return null;

        try
        {
            var process = Process.GetProcessById((int)pid);
            if (process.HasExited) return null;
            var name = process.ProcessName;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}

public sealed class ProductivityDayRecord
{
    public string Date { get; set; } = "";
    public Dictionary<string, int> AppSeconds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? AiSummary { get; set; }
    public DateTime? SummaryUpdatedUtc { get; set; }
}

public readonly record struct ProductivityAppUsage(string AppName, int Seconds);
