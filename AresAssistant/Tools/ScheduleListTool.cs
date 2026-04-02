using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public class ScheduleListTool(ScheduledTaskStore store) : ITool
{
    public string Name => "schedule_list";
    public string Description => "Lista las tareas programadas diarias de ARES.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new(),
        Required = new()
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var items = store.GetAll();
        if (items.Count == 0)
            return Task.FromResult(new ToolResult(true, "No hay tareas programadas."));

        var lines = items.Select(i =>
            $"[{i.Id}] {i.Time} | {(i.Enabled ? "ON" : "OFF")} | {i.Command}" +
            (string.IsNullOrWhiteSpace(i.Description) ? "" : $" | {i.Description}"));

        return Task.FromResult(new ToolResult(true, string.Join(Environment.NewLine, lines)));
    }
}
