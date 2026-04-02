using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public class ActionHistoryTool : ITool
{
    public string Name => "action_history";
    public string Description => "Devuelve las acciones recientes ejecutadas por ARES según el log local.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["max_lines"] = new() { Type = "integer", Description = "Número máximo de líneas", Default = 20 }
        },
        Required = new()
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var maxLines = args.TryGetValue("max_lines", out var m) ? Math.Clamp(m.Value<int>(), 1, 200) : 20;

        var logsDir = Path.GetFullPath("data/logs");
        if (!Directory.Exists(logsDir))
            return Task.FromResult(new ToolResult(true, "No hay historial de acciones todavía."));

        var latest = Directory.GetFiles(logsDir, "actions_*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        if (latest == null)
            return Task.FromResult(new ToolResult(true, "No hay historial de acciones todavía."));

        var lines = File.ReadLines(latest).Reverse().Take(maxLines).Reverse().ToList();
        if (lines.Count == 0)
            return Task.FromResult(new ToolResult(true, "No hay acciones registradas."));

        return Task.FromResult(new ToolResult(true, string.Join(Environment.NewLine, lines)));
    }
}
