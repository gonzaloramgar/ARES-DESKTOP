using System.Diagnostics;
using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

/// <summary>
/// Replaces the 250+ individual open_app_* tools with a single tool that does fuzzy name lookup.
/// This reduces the tool payload sent to the model from ~45 KB to ~3 KB.
/// </summary>
public class GenericOpenAppTool : ITool
{
    public string Name => "open_app";
    public string Description =>
        "Abre una aplicación instalada en el sistema. " +
        "Escribe el nombre de la app tal como la conoces (ej: 'Chrome', 'Notepad', 'Visual Studio Code', 'Bluestacks'). " +
        "El sistema hará una búsqueda aproximada si el nombre no es exacto.";

    // toolKey (e.g. "open_chrome") -> executable path
    private readonly Dictionary<string, string> _paths;
    // toolKey -> human display name
    private readonly Dictionary<string, string> _displayNames;

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["name"] = new() { Type = "string", Description = "Nombre de la aplicación a abrir (ej: 'Chrome', 'Notepad', 'Audacity')" }
        },
        Required = new() { "name" }
    };

    public GenericOpenAppTool(Dictionary<string, string> paths, Dictionary<string, string> displayNames)
    {
        _paths = paths;
        _displayNames = displayNames;
    }

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var name = args.TryGetValue("name", out var n) ? n.ToString().Trim() : "";
        if (string.IsNullOrEmpty(name))
            return Task.FromResult(new ToolResult(false, "Nombre no especificado."));

        var match = FindMatch(name);
        if (match == null)
            return Task.FromResult(new ToolResult(false, $"Aplicación '{name}' no encontrada en el sistema."));

        try
        {
            Process.Start(new ProcessStartInfo { FileName = match.Value.path, UseShellExecute = true });
            return Task.FromResult(new ToolResult(true, $"Aplicación abierta: {match.Value.displayName}."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"Error al abrir la aplicación: {ex.Message}"));
        }
    }

    private (string path, string displayName)? FindMatch(string query)
    {
        // Normalize query: lowercase, spaces → underscores
        var normalized = query.ToLowerInvariant().Trim().Replace(' ', '_');

        // 1. Exact key match (with or without "open_" prefix)
        var keyWithPrefix = normalized.StartsWith("open_") ? normalized : "open_" + normalized;
        if (_paths.TryGetValue(keyWithPrefix, out var p1))
            return (p1, _displayNames.GetValueOrDefault(keyWithPrefix, query));

        // 2. Key contains query
        var keyMatch = _paths.FirstOrDefault(kv =>
            kv.Key.Contains(normalized, StringComparison.OrdinalIgnoreCase));
        if (keyMatch.Key != null)
            return (keyMatch.Value, _displayNames.GetValueOrDefault(keyMatch.Key, query));

        // 3. Display name contains original query (spaces preserved)
        var displayQuery = query.ToLowerInvariant().Trim();
        var nameMatch = _displayNames.FirstOrDefault(kv =>
            kv.Value.Contains(displayQuery, StringComparison.OrdinalIgnoreCase));
        if (nameMatch.Key != null && _paths.TryGetValue(nameMatch.Key, out var p3))
            return (p3, nameMatch.Value);

        return null;
    }
}
