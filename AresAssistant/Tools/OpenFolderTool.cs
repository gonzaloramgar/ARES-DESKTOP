using AresAssistant.Models;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public class OpenFolderTool : ITool
{
    public string Name { get; }
    public string Description { get; }
    private readonly string _path;

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new(),
        Required = new()
    };

    public OpenFolderTool(string name, string displayName, string path)
    {
        Name = name;
        Description = $"Abre la carpeta {displayName}";
        _path = path;
    }

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = _path,
                UseShellExecute = true
            });
            return Task.FromResult(new ToolResult(true, "Carpeta abierta en el explorador."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"Error al abrir carpeta: {ex.Message}"));
        }
    }
}
