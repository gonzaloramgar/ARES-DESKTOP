using AresAssistant.Core;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AresAssistant.Tools;

/// <summary>
/// Herramienta para listar todas las ventanas abiertas con título visible.
/// </summary>
public class ListWindowsTool : ITool
{
    public string Name => "list_open_windows";
    public string Description => "Lista las ventanas abiertas en el sistema.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new(),
        Required = new()
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var windows = Process.GetProcesses()
            .Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(p.MainWindowTitle))
            .Select(p => $"{p.ProcessName}: {p.MainWindowTitle}")
            .ToList();

        return Task.FromResult(new ToolResult(true,
            windows.Count == 0
                ? "No se encontraron ventanas abiertas."
                : string.Join("\n", windows)));
    }
}

/// <summary>
/// Herramienta para minimizar una ventana abierta buscando por título parcial.
/// </summary>
public class MinimizeWindowTool : ITool
{
    public string Name => "minimize_window";
    public string Description => "Minimiza una ventana abierta por su título.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["title"] = new() { Type = "string", Description = "Título parcial o completo de la ventana" }
        },
        Required = new() { "title" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var title = args.TryGetValue("title", out var t) ? t.ToString() : "";
        var proc = Process.GetProcesses()
            .FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero &&
                                 p.MainWindowTitle.Contains(title, StringComparison.OrdinalIgnoreCase));

        if (proc == null)
            return Task.FromResult(new ToolResult(false, $"No se encontró ventana con título '{title}'."));

        WindowNativeMethods.ShowWindow(proc.MainWindowHandle, WindowNativeMethods.SW_MINIMIZE);
        return Task.FromResult(new ToolResult(true, $"Ventana minimizada: {proc.MainWindowTitle}"));
    }
}

/// <summary>
/// Herramienta para maximizar una ventana abierta buscando por título parcial.
/// </summary>
public class MaximizeWindowTool : ITool
{
    public string Name => "maximize_window";
    public string Description => "Maximiza una ventana abierta por su título.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["title"] = new() { Type = "string", Description = "Título parcial o completo de la ventana" }
        },
        Required = new() { "title" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var title = args.TryGetValue("title", out var t) ? t.ToString() : "";
        var proc = Process.GetProcesses()
            .FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero &&
                                 p.MainWindowTitle.Contains(title, StringComparison.OrdinalIgnoreCase));

        if (proc == null)
            return Task.FromResult(new ToolResult(false, $"No se encontró ventana con título '{title}'."));

        WindowNativeMethods.ShowWindow(proc.MainWindowHandle, WindowNativeMethods.SW_MAXIMIZE);
        return Task.FromResult(new ToolResult(true, $"Ventana maximizada: {proc.MainWindowTitle}"));
    }
}
