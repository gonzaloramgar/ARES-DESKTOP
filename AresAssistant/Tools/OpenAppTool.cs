using AresAssistant.Models;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public class OpenAppTool : ITool
{
    public string Name { get; }
    public string Description { get; }
    public string Path { get; }

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new(),
        Required = new()
    };

    public OpenAppTool(string name, string displayName, string path)
    {
        Name = name;
        Description = $"Abre la aplicación {displayName}";
        Path = path;
    }

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = Path,
                UseShellExecute = true
            });
            return Task.FromResult(new ToolResult(true, $"Aplicación abierta correctamente."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"Error al abrir la aplicación: {ex.Message}"));
        }
    }
}
