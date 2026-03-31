using AresAssistant.Core;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace AresAssistant.Tools;

/// <summary>
/// Herramienta para cerrar aplicaciones abiertas por nombre de ventana o proceso.
/// Busca coincidencias parciales (case-insensitive) en título y nombre de proceso.
/// </summary>
public class CloseAppTool : ITool
{
    public string Name => "close_app";
    public string Description => "Cierra una aplicación abierta por su nombre de ventana o proceso";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["app_name"] = new() { Type = "string", Description = "Nombre de la ventana o proceso a cerrar" }
        },
        Required = new() { "app_name" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var appName = args.TryGetValue("app_name", out var v) ? v.ToString() : "";

        var processes = Process.GetProcesses()
            .Where(p =>
                p.ProcessName.Contains(appName, StringComparison.OrdinalIgnoreCase) ||
                (p.MainWindowHandle != IntPtr.Zero &&
                 !string.IsNullOrEmpty(p.MainWindowTitle) &&
                 p.MainWindowTitle.Contains(appName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (processes.Count == 0)
            return Task.FromResult(new ToolResult(false, $"No se encontró ningún proceso que coincida con '{appName}'."));

        int closed = 0;
        foreach (var p in processes)
        {
            try { p.CloseMainWindow(); closed++; }
            catch { /* ignore individual failures */ }
        }

        return Task.FromResult(new ToolResult(true, $"Solicitud de cierre enviada a {closed} proceso(s)."));
    }
}
