using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

/// <summary>
/// Herramienta para crear carpetas. Soporta alias (Desktop, Documents…)
/// y bloquea la creación en directorios del sistema por seguridad.
/// </summary>
public class CreateFolderTool : ITool
{
    public string Name => "create_folder";
    public string Description => "Crea una carpeta en la ruta especificada. Puedes usar alias como 'Desktop', 'Documents', 'Downloads'.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["path"] = new() { Type = "string", Description = "Ruta donde crear la carpeta. Ejemplos: 'Desktop/MiCarpeta', 'C:/Users/grg30/Desktop/Proyecto', 'Documents/Notas'" }
        },
        Required = new() { "path" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var rawPath = args.TryGetValue("path", out var p) ? p.ToString().Trim() : "";
        if (string.IsNullOrEmpty(rawPath))
            return Task.FromResult(new ToolResult(false, "Ruta no especificada."));

        var resolved = PathResolver.Resolve(rawPath);
        try { resolved = Path.GetFullPath(resolved); }
        catch { return Task.FromResult(new ToolResult(false, "Ruta inválida.")); }

        // Safety: never create inside Windows or System32
        var blocked = new[] { @"C:\Windows", @"C:\System", Environment.GetFolderPath(Environment.SpecialFolder.System) };
        if (blocked.Any(b => resolved.StartsWith(Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase)))
            return Task.FromResult(new ToolResult(false, "No se puede crear carpetas en rutas del sistema."));

        try
        {
            Directory.CreateDirectory(resolved);
            return Task.FromResult(new ToolResult(true, $"Carpeta creada: {resolved}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"Error al crear carpeta: {ex.Message}"));
        }
    }
}
