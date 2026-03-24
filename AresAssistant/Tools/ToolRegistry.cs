using AresAssistant.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new();

    public void Register(ITool tool) => _tools[tool.Name] = tool;

    public ITool? Get(string name) => _tools.TryGetValue(name, out var t) ? t : null;

    public List<ToolDefinition> GetToolDefinitions() =>
        _tools.Values.Select(t => new ToolDefinition
        {
            Type = "function",
            Function = new ToolFunction
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = t.Parameters
            }
        }).ToList();

    public void LoadFromJson(string toolsJsonPath)
    {
        if (!File.Exists(toolsJsonPath)) return;

        var json = File.ReadAllText(toolsJsonPath);
        var dict = JsonConvert.DeserializeObject<Dictionary<string, JObject>>(json);
        if (dict == null) return;

        foreach (var (key, obj) in dict)
        {
            var type = obj["type"]?.ToString();
            var displayName = obj["display_name"]?.ToString() ?? key;

            ITool? tool = type switch
            {
                "open_app" => new OpenAppTool(key, displayName, obj["path"]?.ToString() ?? ""),
                "open_folder" => new OpenFolderTool(key, displayName, obj["path"]?.ToString() ?? ""),
                "search_browser" => new SearchBrowserTool(key, displayName, obj["browser_path"]?.ToString() ?? ""),
                _ => null
            };

            if (tool != null)
                Register(tool);
        }
    }

    public IEnumerable<ITool> GetAll() => _tools.Values;
}
