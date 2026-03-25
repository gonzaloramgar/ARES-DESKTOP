using AresAssistant.Core;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public class DeleteFolderTool : ITool
{
    public string Name => "delete_folder";
    public string Description => "Elimina una carpeta y todo su contenido permanentemente. Esta acción no se puede deshacer.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["path"] = new() { Type = "string", Description = "Ruta de la carpeta a eliminar. Ejemplo: 'Desktop/MiCarpeta', 'Documents/Viejo'" }
        },
        Required = new() { "path" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var rawPath = args.TryGetValue("path", out var p) ? p.ToString().Trim() : "";
        if (string.IsNullOrEmpty(rawPath))
            return Task.FromResult(new ToolResult(false, "Ruta no especificada."));

        var resolved = PathResolver.Resolve(rawPath);

        var blocked = new[] { @"C:\Windows", @"C:\System", Environment.GetFolderPath(Environment.SpecialFolder.System) };
        if (blocked.Any(b => resolved.StartsWith(b, StringComparison.OrdinalIgnoreCase)))
            return Task.FromResult(new ToolResult(false, "No se puede eliminar carpetas del sistema."));

        if (!Directory.Exists(resolved))
            return Task.FromResult(new ToolResult(false, $"La carpeta no existe: {resolved}"));

        try
        {
            FileSystem.DeleteDirectory(resolved, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            return Task.FromResult(new ToolResult(true, $"Carpeta movida a la papelera: {resolved}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"Error al eliminar carpeta: {ex.Message}"));
        }
    }
}
