using AresAssistant.Models;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Web;

namespace AresAssistant.Tools;

public class SearchBrowserTool : ITool
{
    public string Name { get; }
    public string Description { get; }
    private readonly string _browserPath;

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["query"] = new() { Type = "string", Description = "Texto a buscar" }
        },
        Required = new() { "query" }
    };

    public SearchBrowserTool(string name, string displayName, string browserPath)
    {
        Name = name;
        Description = $"Busca en Google usando {displayName}";
        _browserPath = browserPath;
    }

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var query = args.TryGetValue("query", out var q) ? q.ToString() : "";
        var url = $"https://www.google.com/search?q={HttpUtility.UrlEncode(query)}";

        try
        {
            Process.Start(new ProcessStartInfo(_browserPath, url) { UseShellExecute = true });
            return Task.FromResult(new ToolResult(true, $"Búsqueda '{query}' abierta."));
        }
        catch
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return Task.FromResult(new ToolResult(true, $"Búsqueda '{query}' abierta en navegador predeterminado."));
        }
    }
}
