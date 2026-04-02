using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public class MemoryReadTool(PersistentMemoryStore memoryStore) : ITool
{
    public string Name => "memory_read";
    public string Description => "Consulta la memoria persistente de ARES para recuperar contexto entre sesiones.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["query"] = new() { Type = "string", Description = "Filtro opcional por texto" },
            ["max_items"] = new() { Type = "integer", Description = "Máximo de entradas a devolver", Default = 10 }
        },
        Required = new()
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var query = args.TryGetValue("query", out var q) ? q.ToString().Trim() : "";
        var maxItems = args.TryGetValue("max_items", out var m) ? Math.Clamp(m.Value<int>(), 1, 50) : 10;

        var items = memoryStore.GetAll();
        if (!string.IsNullOrWhiteSpace(query))
        {
            items = items
                .Where(i => i.Note.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || i.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var top = items.Take(maxItems).ToList();
        if (top.Count == 0)
            return Task.FromResult(new ToolResult(true, "Sin resultados en memoria."));

        var lines = top.Select(i => $"[{i.Category}] {i.Note}");
        return Task.FromResult(new ToolResult(true, string.Join(Environment.NewLine, lines)));
    }
}
