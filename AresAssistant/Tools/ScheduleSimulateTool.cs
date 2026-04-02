using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public class ScheduleSimulateTool(ScheduledTaskStore store, PermissionManager permissionManager) : ITool
{
    public string Name => "schedule_simulate";
    public string Description => "Simula tareas programadas sin ejecutar acciones reales. Valida permisos, siguiente ejecución y riesgo.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["id"] = new() { Type = "string", Description = "Id opcional de tarea concreta" },
            ["hours_ahead"] = new() { Type = "integer", Description = "Ventana de simulación en horas", Default = 24 }
        },
        Required = new()
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var id = args.TryGetValue("id", out var idTok) ? idTok.ToString().Trim() : "";
        var hoursAhead = args.TryGetValue("hours_ahead", out var hTok) ? Math.Clamp(hTok.Value<int>(), 1, 168) : 24;

        var tasks = store.GetAll().Where(t => t.Enabled).ToList();
        if (!string.IsNullOrWhiteSpace(id))
            tasks = tasks.Where(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).ToList();

        if (tasks.Count == 0)
            return Task.FromResult(new ToolResult(true, "No hay tareas activas para simular."));

        var now = DateTime.Now;
        var horizon = now.AddHours(hoursAhead);
        var lines = new List<string>
        {
            $"Simulación scheduler (sin ejecución real), ventana: ahora -> +{hoursAhead}h"
        };

        foreach (var task in tasks)
        {
            var nextRun = ComputeNextRun(task.Time, now);
            var willRun = nextRun <= horizon;
            var level = permissionManager.GetLevel("run_command", new Dictionary<string, JToken>
            {
                ["command"] = task.Command
            });

            var risk = level switch
            {
                PermissionLevel.Blocked => "BLOQUEADO",
                PermissionLevel.Confirm => "CONFIRMACIÓN",
                _ => "AUTO"
            };

            lines.Add($"[{task.Id}] {task.Time} | {task.Command}");
            lines.Add($"  - Próxima ejecución: {nextRun:yyyy-MM-dd HH:mm}");
            lines.Add($"  - En ventana: {(willRun ? "sí" : "no")}");
            lines.Add($"  - Permiso estimado: {risk}");
            if (!string.IsNullOrWhiteSpace(task.Description))
                lines.Add($"  - Descripción: {task.Description}");
        }

        return Task.FromResult(new ToolResult(true, string.Join(Environment.NewLine, lines)));
    }

    private static DateTime ComputeNextRun(string hhmm, DateTime now)
    {
        if (!TimeOnly.TryParseExact(hhmm, "HH:mm", out var time))
            return now;

        var candidate = now.Date.Add(time.ToTimeSpan());
        if (candidate <= now)
            candidate = candidate.AddDays(1);
        return candidate;
    }
}
