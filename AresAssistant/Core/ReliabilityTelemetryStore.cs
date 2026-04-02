using Newtonsoft.Json;

namespace AresAssistant.Core;

public sealed class ReliabilityTelemetryStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private ReliabilityTelemetrySnapshot _snapshot = new();

    public bool Enabled { get; set; }

    public ReliabilityTelemetryStore(string path, bool enabled)
    {
        _path = path;
        Enabled = enabled;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        Load();
    }

    public void RecordTool(string toolName, bool success, long elapsedMs)
    {
        if (!Enabled) return;
        toolName = string.IsNullOrWhiteSpace(toolName) ? "unknown" : toolName.Trim();

        lock (_lock)
        {
            var stat = GetOrCreateTool(toolName);
            stat.Total++;
            if (success) stat.Success++; else stat.Failed++;
            stat.LastElapsedMs = elapsedMs;
            stat.LastSeenUtc = DateTime.UtcNow;
            Save();
        }
    }

    public void RecordModel(string model, bool success, long elapsedMs)
    {
        if (!Enabled) return;
        model = string.IsNullOrWhiteSpace(model) ? "unknown" : model.Trim();

        lock (_lock)
        {
            var stat = GetOrCreateModel(model);
            stat.Total++;
            if (success) stat.Success++; else stat.Failed++;
            stat.LastElapsedMs = elapsedMs;
            stat.LastSeenUtc = DateTime.UtcNow;
            Save();
        }
    }

    public string BuildWeeklyTopIssuesReport(int top = 3)
    {
        lock (_lock)
        {
            var since = DateTime.UtcNow.AddDays(-7);
            var tools = _snapshot.Tools.Values
                .Where(t => t.LastSeenUtc >= since && t.Failed > 0)
                .OrderByDescending(t => t.Failed)
                .Take(Math.Max(1, top))
                .ToList();

            var models = _snapshot.Models.Values
                .Where(m => m.LastSeenUtc >= since && m.Failed > 0)
                .OrderByDescending(m => m.Failed)
                .Take(Math.Max(1, top))
                .ToList();

            var lines = new List<string>
            {
                "Telemetría de fiabilidad (últimos 7 días)"
            };

            if (tools.Count == 0 && models.Count == 0)
            {
                lines.Add("- Sin fallos registrados.");
                return string.Join(Environment.NewLine, lines);
            }

            foreach (var t in tools)
                lines.Add($"- Tool: {t.Name} | fallos={t.Failed}, éxito={t.Success}, último={t.LastElapsedMs}ms");

            foreach (var m in models)
                lines.Add($"- Modelo: {m.Name} | fallos={m.Failed}, éxito={m.Success}, último={m.LastElapsedMs}ms");

            return string.Join(Environment.NewLine, lines);
        }
    }

    private ReliabilityCounter GetOrCreateTool(string name)
    {
        if (_snapshot.Tools.TryGetValue(name, out var existing)) return existing;
        var created = new ReliabilityCounter { Name = name };
        _snapshot.Tools[name] = created;
        return created;
    }

    private ReliabilityCounter GetOrCreateModel(string name)
    {
        if (_snapshot.Models.TryGetValue(name, out var existing)) return existing;
        var created = new ReliabilityCounter { Name = name };
        _snapshot.Models[name] = created;
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
            _snapshot = JsonConvert.DeserializeObject<ReliabilityTelemetrySnapshot>(raw) ?? new ReliabilityTelemetrySnapshot();
        }
        catch
        {
            _snapshot = new ReliabilityTelemetrySnapshot();
            Save();
        }
    }

    private void Save()
    {
        var json = JsonConvert.SerializeObject(_snapshot, Formatting.Indented);
        File.WriteAllText(_path, json);
    }
}

public sealed class ReliabilityTelemetrySnapshot
{
    public Dictionary<string, ReliabilityCounter> Tools { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ReliabilityCounter> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ReliabilityCounter
{
    public string Name { get; set; } = "";
    public int Total { get; set; }
    public int Success { get; set; }
    public int Failed { get; set; }
    public long LastElapsedMs { get; set; }
    public DateTime LastSeenUtc { get; set; }
}
