using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public class ReadFileTool : ITool
{
    public string Name => "read_file";
    public string Description => "Lee el contenido de un archivo de texto. Máximo max_lines líneas.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["path"] = new() { Type = "string", Description = "Ruta absoluta o relativa del archivo a leer" },
            ["max_lines"] = new() { Type = "integer", Description = "Máximo número de líneas a leer", Default = 200 }
        },
        Required = new() { "path" }
    };

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var rawPath = args.TryGetValue("path", out var p) ? p.ToString() : "";
        var path = PathResolver.Resolve(rawPath);
        var maxLines = args.TryGetValue("max_lines", out var m) ? m.Value<int>() : 200;

        if (!File.Exists(path))
            return new ToolResult(false, $"Archivo no encontrado: {path}");

        try
        {
            var lines = new List<string>(maxLines);
            using var reader = new StreamReader(path);
            while (lines.Count < maxLines && await reader.ReadLineAsync() is { } line)
                lines.Add(line);

            var content = string.Join(Environment.NewLine, lines);
            var truncated = lines.Count >= maxLines ? $"\n[...truncado a {maxLines} líneas]" : "";
            return new ToolResult(true, content + truncated);
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Error al leer archivo: {ex.Message}");
        }
    }
}
