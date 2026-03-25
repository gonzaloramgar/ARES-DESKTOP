using AresAssistant.Core;
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

    private const int MaxLength = 500;
    private static readonly string[] DangerousSequences =
    {
        "%{F4}", "^{ESC}", "{DEL}", "+{DEL}", "^+{ESC}",
        "%{TAB}", "^w", "^q", "%{F4}"
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var text = args.TryGetValue("text", out var t) ? t.ToString() : "";

        if (string.IsNullOrEmpty(text))
            return Task.FromResult(new ToolResult(false, "El texto no puede estar vacío."));

        if (text.Length > MaxLength)
            return Task.FromResult(new ToolResult(false, $"Texto demasiado largo ({text.Length} chars). Máximo: {MaxLength}."));

        var lower = text.ToLowerInvariant();
        foreach (var seq in DangerousSequences)
        {
            if (lower.Contains(seq.ToLowerInvariant()))
                return Task.FromResult(new ToolResult(false, $"Secuencia de teclas peligrosa bloqueada: {seq}"));
        }

        try
        {
            SendKeys.SendWait(text);
            return Task.FromResult(new ToolResult(true, "Texto enviado correctamente."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"Error al enviar texto: {ex.Message}"));
        }
    }
}
