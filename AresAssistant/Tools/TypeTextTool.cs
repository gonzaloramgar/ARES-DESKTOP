using AresAssistant.Models;
using Newtonsoft.Json.Linq;
using System.Windows.Forms;

namespace AresAssistant.Tools;

public class TypeTextTool : ITool
{
    public string Name => "type_text";
    public string Description => "Escribe texto como si lo tecleara el usuario en la aplicación activa.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["text"] = new() { Type = "string", Description = "Texto a escribir" }
        },
        Required = new() { "text" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var text = args.TryGetValue("text", out var t) ? t.ToString() : "";
        try
        {
            SendKeys.SendWait(text);
            return Task.FromResult(new ToolResult(true, $"Texto enviado correctamente."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"Error al enviar texto: {ex.Message}"));
        }
    }
}
