using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

/// <summary>
/// Allows the user to tell ARES about a non-official app / game.
/// The name + path are stored in data/custom-apps.json and hot-loaded
/// into the running GenericOpenAppTool so the app can be opened immediately.
/// </summary>
public class RememberAppTool : ITool
{
    private readonly ToolRegistry _registry;

    public string Name => "remember_app";
    public string Description =>
        "Guarda el nombre y la ruta de una aplicación o juego que no se detectó automáticamente. " +
        "Así ARES la recordará para siempre y podrá abrirla con open_app en el futuro.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["name"] = new() { Type = "string", Description = "Nombre corto de la app/juego (ej: 'Minecraft', 'osu!')" },
            ["path"] = new() { Type = "string", Description = "Ruta completa al .exe o acceso directo (ej: 'D:\\Games\\Minecraft\\minecraft.exe')" }
        },
        Required = new() { "name", "path" }
    };

    public RememberAppTool(ToolRegistry registry)
    {
        _registry = registry;
    }

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var name = args.TryGetValue("name", out var n) ? n.ToString().Trim() : "";
        var path = args.TryGetValue("path", out var p) ? p.ToString().Trim() : "";

        if (string.IsNullOrEmpty(name))
            return Task.FromResult(new ToolResult(false, "Nombre no especificado."));
        if (string.IsNullOrEmpty(path))
            return Task.FromResult(new ToolResult(false, "Ruta no especificada."));

        // Validate path exists (unless it's a protocol like steam://)
        if (!path.Contains("://") && !System.IO.File.Exists(path))
            return Task.FromResult(new ToolResult(false, $"La ruta no existe: {path}"));

        // Persist to custom-apps.json
        if (!AppScanner.SaveCustomApp(name, path))
            return Task.FromResult(new ToolResult(false, "Error al guardar la app en memoria."));

        // Hot-reload: re-read tools.json + custom apps so open_app finds it immediately
        _registry.LoadFromJson("data/tools.json");

        return Task.FromResult(new ToolResult(true,
            $"'{name}' guardado en memoria. Ahora puedo abrirlo con open_app siempre que quieras."));
    }
}
