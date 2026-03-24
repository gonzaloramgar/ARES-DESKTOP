using AresAssistant.Core;
using Newtonsoft.Json.Linq;
using System.Windows;

namespace AresAssistant.Tools;

public class ClipboardReadTool : ITool
{
    public string Name => "clipboard_read";
    public string Description => "Lee el contenido actual del portapapeles.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new(),
        Required = new()
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        string text = "";
        Application.Current.Dispatcher.Invoke(() => text = Clipboard.GetText());
        return Task.FromResult(new ToolResult(true, string.IsNullOrEmpty(text)
            ? "(portapapeles vacío)"
            : text));
    }
}

public class ClipboardWriteTool : ITool
{
    public string Name => "clipboard_write";
    public string Description => "Escribe texto en el portapapeles.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["text"] = new() { Type = "string", Description = "Texto a copiar al portapapeles" }
        },
        Required = new() { "text" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var text = args.TryGetValue("text", out var t) ? t.ToString() : "";
        Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(text));
        return Task.FromResult(new ToolResult(true, "Texto copiado al portapapeles."));
    }
}
