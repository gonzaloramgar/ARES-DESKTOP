using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public class MemoryWriteTool(PersistentMemoryStore memoryStore) : ITool
{
    public string Name => "memory_write";
    public string Description => "Guarda un hecho importante en la memoria persistente de ARES para futuras sesiones.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["note"] = new() { Type = "string", Description = "Hecho o preferencia a recordar" },
            ["category"] = new() { Type = "string", Description = "Categoría breve: usuario, proyecto, recordatorio, general", Default = "general" },
            ["project_scope"] = new() { Type = "string", Description = "Ámbito de proyecto opcional (ej: Ares). Si no se indica, usa el proyecto actual." },
            ["ttl_days"] = new() { Type = "integer", Description = "Días de vida de la memoria (0 = permanente)", Default = 0 }
        },
        Required = new() { "note" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var note = args.TryGetValue("note", out var n) ? n.ToString() : "";
        var category = args.TryGetValue("category", out var c) ? c.ToString() : "general";
        var scope = args.TryGetValue("project_scope", out var s) ? s.ToString().Trim() : PersistentMemoryStore.GetCurrentProjectScope();
        var ttlDays = args.TryGetValue("ttl_days", out var ttl) ? Math.Max(0, ttl.Value<int>()) : 0;

        if (!memoryStore.Upsert(note, category, scope, ttlDays))
            return Task.FromResult(new ToolResult(false, "No se pudo guardar la memoria: nota vacía."));

        return Task.FromResult(new ToolResult(true, $"Memoria guardada correctamente en ámbito '{scope}'."));
    }
}
