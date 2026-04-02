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
            ["category"] = new() { Type = "string", Description = "Categoría breve: usuario, proyecto, recordatorio, general", Default = "general" }
        },
        Required = new() { "note" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var note = args.TryGetValue("note", out var n) ? n.ToString() : "";
        var category = args.TryGetValue("category", out var c) ? c.ToString() : "general";

        if (!memoryStore.Upsert(note, category))
            return Task.FromResult(new ToolResult(false, "No se pudo guardar la memoria: nota vacía."));

        return Task.FromResult(new ToolResult(true, "Memoria guardada correctamente."));
    }
}
