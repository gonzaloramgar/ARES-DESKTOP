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

    public List<MemoryItem> GetAll(string? projectScope = null, bool includeGeneral = true)
    {
        lock (_lock)
        {
            PruneExpiredLocked();

            if (string.IsNullOrWhiteSpace(projectScope))
                return _items.OrderByDescending(i => i.UpdatedAt).ToList();

            var scope = projectScope.Trim();
            return _items
                .Where(i => i.ProjectScope.Equals(scope, StringComparison.OrdinalIgnoreCase)
                         || (includeGeneral && string.IsNullOrWhiteSpace(i.ProjectScope)))
                .OrderByDescending(i => i.UpdatedAt)
                .ToList();
        }
    }

    public string BuildPromptContext(int maxItems = 8, string? projectScope = null)
    {
        lock (_lock)
        {
            PruneExpiredLocked();
            if (_items.Count == 0) return "(sin memoria persistente guardada)";

            var baseItems = string.IsNullOrWhiteSpace(projectScope)
                ? _items
                : _items.Where(i => i.ProjectScope.Equals(projectScope, StringComparison.OrdinalIgnoreCase)
                                 || string.IsNullOrWhiteSpace(i.ProjectScope)).ToList();

            var lines = baseItems
                .OrderByDescending(i => i.UpdatedAt)
                .Take(Math.Max(1, maxItems))
                .Select(i =>
                {
                    var scopeTag = string.IsNullOrWhiteSpace(i.ProjectScope) ? "general" : i.ProjectScope;
                    return $"- [{i.Category}/{scopeTag}] {i.Note}";
                });

            return string.Join(Environment.NewLine, lines);
        }
    }

    public bool Upsert(string note, string category = "general", string? projectScope = null, int ttlDays = 0)
    {
        note = (note ?? string.Empty).Trim();
        category = string.IsNullOrWhiteSpace(category) ? "general" : category.Trim().ToLowerInvariant();
        projectScope = string.IsNullOrWhiteSpace(projectScope) ? "" : projectScope.Trim();
        if (string.IsNullOrWhiteSpace(note)) return false;

        lock (_lock)
        {
            PruneExpiredLocked();

            var existing = _items.FirstOrDefault(i =>
                i.Note.Equals(note, StringComparison.OrdinalIgnoreCase)
                && i.ProjectScope.Equals(projectScope, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Category = category;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.ExpiresAtUtc = ttlDays > 0 ? DateTime.UtcNow.AddDays(ttlDays) : null;
            }
            else
            {
                _items.Add(new MemoryItem
                {
                    Category = category,
                    Note = note,
                    ProjectScope = projectScope,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ExpiresAtUtc = ttlDays > 0 ? DateTime.UtcNow.AddDays(ttlDays) : null
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

    public bool Forget(string note, string? projectScope = null)
    {
        note = (note ?? string.Empty).Trim();
        projectScope = string.IsNullOrWhiteSpace(projectScope) ? "" : projectScope.Trim();
        if (string.IsNullOrWhiteSpace(note)) return false;

        lock (_lock)
        {
            PruneExpiredLocked();
            var removed = _items.RemoveAll(i =>
                i.Note.Equals(note, StringComparison.OrdinalIgnoreCase)
                && i.ProjectScope.Equals(projectScope, StringComparison.OrdinalIgnoreCase));
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
            lock (_lock)
            {
                _items = data;
                PruneExpiredLocked();
            }
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

    private void PruneExpiredLocked()
    {
        var now = DateTime.UtcNow;
        _items.RemoveAll(i => i.ExpiresAtUtc.HasValue && i.ExpiresAtUtc.Value <= now);
    }

    public static string GetCurrentProjectScope()
    {
        try
        {
            var cwd = Directory.GetCurrentDirectory();
            var name = new DirectoryInfo(cwd).Name;
            return string.IsNullOrWhiteSpace(name) ? "general" : name;
        }
        catch
        {
            return "general";
        }
    }
}

public sealed class MemoryItem
{
    public string Category { get; set; } = "general";
    public string Note { get; set; } = "";
    public string ProjectScope { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}
