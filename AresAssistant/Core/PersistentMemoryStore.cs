using Newtonsoft.Json;

namespace AresAssistant.Core;

public sealed class PersistentMemoryStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private List<MemoryItem> _items = new();

    public int Version { get; private set; }

    public PersistentMemoryStore(string path)
    {
        _path = path;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        Load();
    }

    public List<MemoryItem> GetAll()
    {
        lock (_lock) return _items.OrderByDescending(i => i.UpdatedAt).ToList();
    }

    public string BuildPromptContext(int maxItems = 8)
    {
        lock (_lock)
        {
            if (_items.Count == 0) return "(sin memoria persistente guardada)";

            var lines = _items
                .OrderByDescending(i => i.UpdatedAt)
                .Take(Math.Max(1, maxItems))
                .Select(i => $"- [{i.Category}] {i.Note}");

            return string.Join(Environment.NewLine, lines);
        }
    }

    public bool Upsert(string note, string category = "general")
    {
        note = (note ?? string.Empty).Trim();
        category = string.IsNullOrWhiteSpace(category) ? "general" : category.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(note)) return false;

        lock (_lock)
        {
            var existing = _items.FirstOrDefault(i => i.Note.Equals(note, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Category = category;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _items.Add(new MemoryItem
                {
                    Category = category,
                    Note = note,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // Keep memory compact and editable.
            _items = _items
                .OrderByDescending(i => i.UpdatedAt)
                .Take(200)
                .ToList();

            Save();
            Version++;
            return true;
        }
    }

    public bool Forget(string note)
    {
        note = (note ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(note)) return false;

        lock (_lock)
        {
            var removed = _items.RemoveAll(i => i.Note.Equals(note, StringComparison.OrdinalIgnoreCase));
            if (removed <= 0) return false;
            Save();
            Version++;
            return true;
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
            var data = JsonConvert.DeserializeObject<List<MemoryItem>>(raw) ?? new List<MemoryItem>();
            lock (_lock) _items = data;
        }
        catch
        {
            lock (_lock) _items = new List<MemoryItem>();
            Save();
        }
    }

    private void Save()
    {
        var json = JsonConvert.SerializeObject(_items, Formatting.Indented);
        File.WriteAllText(_path, json);
    }
}

public sealed class MemoryItem
{
    public string Category { get; set; } = "general";
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
