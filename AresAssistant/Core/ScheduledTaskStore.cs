using Newtonsoft.Json;

namespace AresAssistant.Core;

public sealed class ScheduledTaskStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private List<ScheduledTaskItem> _items = new();

    public ScheduledTaskStore(string path)
    {
        _path = path;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        Load();
    }

    public List<ScheduledTaskItem> GetAll()
    {
        lock (_lock) return _items.OrderBy(i => i.Time).ToList();
    }

    public ScheduledTaskItem Add(string time, string command, string description = "")
    {
        if (!IsValidTime(time))
            throw new ArgumentException("Formato de hora inválido. Usa HH:mm");

        var item = new ScheduledTaskItem
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Time = time,
            Command = command,
            Description = description,
            Enabled = true
        };

        lock (_lock)
        {
            _items.Add(item);
            Save();
        }

        return item;
    }

    public bool Remove(string id)
    {
        lock (_lock)
        {
            var removed = _items.RemoveAll(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (removed <= 0) return false;
            Save();
            return true;
        }
    }

    public void Upsert(ScheduledTaskItem item)
    {
        lock (_lock)
        {
            var idx = _items.FindIndex(i => i.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) _items[idx] = item;
            else _items.Add(item);
            Save();
        }
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
            _items = JsonConvert.DeserializeObject<List<ScheduledTaskItem>>(raw) ?? new List<ScheduledTaskItem>();
        }
        catch
        {
            _items = new List<ScheduledTaskItem>();
            Save();
        }
    }

    private void Save()
    {
        var json = JsonConvert.SerializeObject(_items, Formatting.Indented);
        File.WriteAllText(_path, json);
    }

    public static bool IsValidTime(string time)
        => TimeOnly.TryParseExact(time, "HH:mm", out _);
}

public sealed class ScheduledTaskItem
{
    public string Id { get; set; } = "";
    public string Time { get; set; } = "09:00";
    public string Command { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public DateTime? LastRunAt { get; set; }
}
