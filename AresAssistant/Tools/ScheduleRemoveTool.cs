using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public class ScheduleRemoveTool(ScheduledTaskStore store) : ITool
{
    public string Name => "schedule_remove";
    public string Description => "Elimina una tarea programada por su id.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["id"] = new() { Type = "string", Description = "Id de la tarea (ej: a1b2c3d4)" }
        },
        Required = new() { "id" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var id = args.TryGetValue("id", out var t) ? t.ToString() : "";
        if (string.IsNullOrWhiteSpace(id))
            return Task.FromResult(new ToolResult(false, "Debes indicar id."));

        var ok = store.Remove(id);
        return Task.FromResult(ok
            ? new ToolResult(true, "Tarea eliminada.")
            : new ToolResult(false, "No se encontró esa tarea."));
    }
}
