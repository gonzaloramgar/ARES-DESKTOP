using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

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

    private static readonly Dictionary<string, string> KnownAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Desktop"]   = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        ["Documents"] = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        ["Downloads"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
        ["Pictures"]  = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        ["Music"]     = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        ["Videos"]    = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var rawPath = args.TryGetValue("path", out var p) ? p.ToString().Trim() : "";
        if (string.IsNullOrEmpty(rawPath))
            return Task.FromResult(new ToolResult(false, "Ruta no especificada."));

        var resolved = ResolvePath(rawPath);

        // Safety: never create inside Windows or System32
        var blocked = new[] { @"C:\Windows", @"C:\System", Environment.GetFolderPath(Environment.SpecialFolder.System) };
        if (blocked.Any(b => resolved.StartsWith(b, StringComparison.OrdinalIgnoreCase)))
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

    private static string ResolvePath(string input)
    {
        // Replace forward slashes with backslashes
        input = input.Replace('/', Path.DirectorySeparatorChar);

        // Check if starts with a known alias
        var parts = input.Split(Path.DirectorySeparatorChar, 2);
        if (KnownAliases.TryGetValue(parts[0], out var basePath))
            return parts.Length > 1 ? Path.Combine(basePath, parts[1]) : basePath;

        return input;
    }
}
