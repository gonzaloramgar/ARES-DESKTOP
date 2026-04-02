using AresAssistant.Tools;
using Newtonsoft.Json;

namespace AresAssistant.Core;

public sealed class PluginToolLoader
{
    public int LoadIntoRegistry(string pluginsDirectory, ToolRegistry registry)
    {
        Directory.CreateDirectory(pluginsDirectory);
        var loaded = 0;

        foreach (var file in Directory.GetFiles(pluginsDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var raw = File.ReadAllText(file);
                var manifest = JsonConvert.DeserializeObject<PluginToolManifest>(raw);
                if (manifest == null || !manifest.Enabled) continue;
                if (!string.Equals(manifest.Type, "external-command", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.IsNullOrWhiteSpace(manifest.Name) || string.IsNullOrWhiteSpace(manifest.Command)) continue;

                if (!manifest.Name.StartsWith("plugin_", StringComparison.OrdinalIgnoreCase))
                    manifest.Name = "plugin_" + manifest.Name.Trim();

                registry.Register(new ExternalCommandPluginTool(manifest));
                loaded++;
            }
            catch
            {
                // Ignore invalid plugin manifest and continue loading others.
            }
        }

        return loaded;
    }
}

public sealed class PluginToolManifest
{
    public bool Enabled { get; set; } = true;
    public string Type { get; set; } = "external-command";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Command { get; set; } = "";
    public string ArgumentsTemplate { get; set; } = "";
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
