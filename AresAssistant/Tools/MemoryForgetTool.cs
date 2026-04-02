using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public class MemoryForgetTool(PersistentMemoryStore memoryStore) : ITool
{
    public string Name => "memory_forget";
    public string Description => "Elimina una entrada exacta de la memoria persistente.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["note"] = new() { Type = "string", Description = "Texto exacto de la nota a eliminar" },
            ["project_scope"] = new() { Type = "string", Description = "Ámbito de proyecto opcional. Si no se indica, usa el proyecto actual." }
        },
        Required = new() { "note" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var note = args.TryGetValue("note", out var n) ? n.ToString() : "";
        var scope = args.TryGetValue("project_scope", out var s) ? s.ToString().Trim() : PersistentMemoryStore.GetCurrentProjectScope();
        var ok = memoryStore.Forget(note, scope);
        return Task.FromResult(ok
            ? new ToolResult(true, "Memoria eliminada.")
            : new ToolResult(false, "No se encontró esa nota en memoria."));
    }
}
