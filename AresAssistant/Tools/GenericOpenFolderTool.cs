using System.Diagnostics;
using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

/// <summary>
/// Replaces the 15 individual open_folder_* tools with a single tool.
/// Supports PathResolver aliases, scanned shortcuts, and arbitrary paths.
/// </summary>
public class GenericOpenFolderTool : ITool
{
    public string Name => "open_folder";
    public string Description =>
        "Abre el Explorador de Windows en la carpeta indicada. " +
        "Acepta rutas absolutas, alias estándar (Desktop, Documents, Downloads, Pictures, Music, Videos) " +
        "o nombres de accesos directos del sistema.";

    // shortcut key (e.g. "open_documents_folder") -> path
    private readonly Dictionary<string, string> _shortcuts;
    // display name -> path for fuzzy matching
    private readonly Dictionary<string, string> _byDisplayName;

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["path"] = new() { Type = "string", Description = "Ruta o nombre de carpeta (ej: 'Desktop', 'Documents', 'C:/Users/...')" }
        },
        Required = new() { "path" }
    };

    public GenericOpenFolderTool(Dictionary<string, string> shortcuts, Dictionary<string, string> byDisplayName)
    {
        _shortcuts = shortcuts;
        _byDisplayName = byDisplayName;
    }

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var input = args.TryGetValue("path", out var p) ? p.ToString().Trim() : "";
        if (string.IsNullOrEmpty(input))
            return Task.FromResult(new ToolResult(false, "Ruta no especificada."));

        var resolved = Resolve(input);

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", resolved) { UseShellExecute = true });
            return Task.FromResult(new ToolResult(true, $"Carpeta abierta: {resolved}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"Error al abrir carpeta: {ex.Message}"));
        }
    }

    private string Resolve(string input)
    {
        // 1. Try PathResolver aliases (Desktop, Documents, Downloads, etc.)
        var aliasResolved = PathResolver.Resolve(input);
        if (aliasResolved != input) return aliasResolved;

        // 2. Exact shortcut key
        if (_shortcuts.TryGetValue(input, out var exact)) return exact;

        // 3. Fuzzy: key or display name contains query
        var lower = input.ToLowerInvariant();
        var shortcutMatch = _shortcuts.FirstOrDefault(kv =>
            kv.Key.Contains(lower, StringComparison.OrdinalIgnoreCase));
        if (shortcutMatch.Key != null) return shortcutMatch.Value;

        var displayMatch = _byDisplayName.FirstOrDefault(kv =>
            kv.Key.Contains(lower, StringComparison.OrdinalIgnoreCase));
        if (displayMatch.Value != null) return displayMatch.Value;

        // 4. Fall through: treat as raw path
        return input;
    }
}
