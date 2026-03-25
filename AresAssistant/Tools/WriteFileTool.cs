using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public class WriteFileTool : ITool
{
    public string Name => "write_file";
    public string Description => "Escribe contenido completo en un archivo de texto.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["path"] = new() { Type = "string", Description = "Ruta del archivo a escribir" },
            ["content"] = new() { Type = "string", Description = "Contenido a escribir en el archivo" }
        },
        Required = new() { "path", "content" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var rawPath = args.TryGetValue("path", out var p) ? p.ToString() : "";
        var path = PathResolver.Resolve(rawPath);
        var content = args.TryGetValue("content", out var c) ? c.ToString() : "";

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, content);
            return Task.FromResult(new ToolResult(true, $"Archivo escrito correctamente: {path}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"Error al escribir archivo: {ex.Message}"));
        }
    }
}
