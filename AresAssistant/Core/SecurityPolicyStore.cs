using Newtonsoft.Json;

namespace AresAssistant.Core;

public sealed class SecurityPolicyStore
{
    private readonly string _path;
    public SecurityPolicy Policy { get; private set; } = new();

    public SecurityPolicyStore(string path)
    {
        _path = path;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        Load();
    }

    public void Reload() => Load();

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
            Policy = JsonConvert.DeserializeObject<SecurityPolicy>(raw) ?? new SecurityPolicy();
        }
        catch
        {
            Policy = new SecurityPolicy();
            Save();
        }
    }

    private void Save()
    {
        var json = JsonConvert.SerializeObject(Policy, Formatting.Indented);
        File.WriteAllText(_path, json);
    }
}

public sealed class SecurityPolicy
{
    public List<string> BlockedTools { get; set; } = new();
    public List<string> ConfirmTools { get; set; } = new();
    public List<string> BlockedCommandPatterns { get; set; } = new();
    public List<string> BlockedPathPrefixes { get; set; } = new();
    public List<string> AllowedPluginTools { get; set; } = new();
}
