using AresAssistant.Core;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Web;

namespace AresAssistant.Tools;

public class SearchWebTool : ITool
{
    public string Name => "search_web";
    public string Description => "Realiza una búsqueda en Google abriéndola en el navegador predeterminado.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["query"] = new() { Type = "string", Description = "Texto a buscar en Google" }
        },
        Required = new() { "query" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var query = args.TryGetValue("query", out var q) ? q.ToString() : "";
        var encoded = HttpUtility.UrlEncode(query);
        var url = $"https://www.google.com/search?q={encoded}";

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        return Task.FromResult(new ToolResult(true, $"Búsqueda abierta: {query}"));
    }
}
