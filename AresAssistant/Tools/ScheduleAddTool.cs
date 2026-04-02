using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public class ScheduleAddTool(ScheduledTaskStore store) : ITool
{
    public string Name => "schedule_add";
    public string Description => "Programa una tarea diaria a una hora HH:mm ejecutando un comando permitido.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["time"] = new() { Type = "string", Description = "Hora diaria en formato HH:mm" },
            ["command"] = new() { Type = "string", Description = "Comando permitido para run_command" },
            ["description"] = new() { Type = "string", Description = "Descripción opcional" }
        },
        Required = new() { "time", "command" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var time = args.TryGetValue("time", out var t) ? t.ToString() : "";
        var command = args.TryGetValue("command", out var c) ? c.ToString() : "";
        var description = args.TryGetValue("description", out var d) ? d.ToString() : "";

        if (!ScheduledTaskStore.IsValidTime(time))
            return Task.FromResult(new ToolResult(false, "Hora inválida. Usa HH:mm"));

        if (string.IsNullOrWhiteSpace(command))
            return Task.FromResult(new ToolResult(false, "Debes indicar un comando."));

        var item = store.Add(time, command, description);
        return Task.FromResult(new ToolResult(true, $"Tarea programada [{item.Id}] a las {item.Time}."));
    }
}
