using AresAssistant.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new();
    // Cached definition list; invalidated on Register/LoadFromJson
    private List<ToolDefinition>? _cachedDefinitions;

    public void Register(ITool tool)
    {
        _tools[tool.Name] = tool;
        _cachedDefinitions = null;
    }

    public ITool? Get(string name) => _tools.TryGetValue(name, out var t) ? t : null;

    public List<ToolDefinition> GetToolDefinitions() =>
        _cachedDefinitions ??= _tools.Values.Select(t => new ToolDefinition
        {
            Type = "function",
            Function = new ToolFunction
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = t.Parameters
            }
        }).ToList();

    /// <summary>
    /// Loads scanned tools from tools.json.
    /// Instead of registering one tool per app (which sends 250+ definitions to the model on every
    /// request), all open_app entries are collapsed into a single GenericOpenAppTool and all
    /// open_folder entries into a single GenericOpenFolderTool. This reduces the tool payload from
    /// ~45 KB to ~3 KB per request, dramatically cutting prompt-evaluation time.
    /// </summary>
    public void LoadFromJson(string toolsJsonPath)
    {
        if (!File.Exists(toolsJsonPath)) return;

        var json = File.ReadAllText(toolsJsonPath);
        var dict = JsonConvert.DeserializeObject<Dictionary<string, JObject>>(json);
        if (dict == null) return;

        var appPaths        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var appDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var folderPaths     = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var folderNames     = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, obj) in dict)
        {
            var type        = obj["type"]?.ToString();
            var displayName = obj["display_name"]?.ToString() ?? key;

            switch (type)
            {
                case "open_app":
                    var appPath = obj["path"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(appPath))
                    {
                        appPaths[key]        = appPath;
                        appDisplayNames[key] = displayName;
                    }
                    break;

                case "open_folder":
                    var folderPath = obj["path"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(folderPath))
                    {
                        folderPaths[key]  = folderPath;
                        folderNames[displayName] = folderPath;
                    }
                    break;

                case "search_browser":
                    // Only a handful of these; register individually
                    Register(new SearchBrowserTool(key, displayName, obj["browser_path"]?.ToString() ?? ""));
                    break;
            }
        }

        if (appPaths.Count > 0)
            Register(new GenericOpenAppTool(appPaths, appDisplayNames));

        if (folderPaths.Count > 0)
            Register(new GenericOpenFolderTool(folderPaths, folderNames));
    }

    public IEnumerable<ITool> GetAll() => _tools.Values;
}
