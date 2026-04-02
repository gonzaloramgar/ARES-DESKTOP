using System.Diagnostics;
using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public class ExternalCommandPluginTool(PluginToolManifest manifest) : ITool
{
    public string Name => manifest.Name.Trim();
    public string Description => string.IsNullOrWhiteSpace(manifest.Description)
        ? "Herramienta plugin externa"
        : manifest.Description;

    public ToolParameterSchema Parameters { get; } = BuildSchema(manifest);

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        try
        {
            var arguments = manifest.ArgumentsTemplate ?? string.Empty;
            foreach (var (key, _) in manifest.Parameters)
            {
                var value = args.TryGetValue(key, out var tok) ? tok.ToString() : "";
                arguments = arguments.Replace("{" + key + "}", Escape(value), StringComparison.OrdinalIgnoreCase);
            }

            var psi = new ProcessStartInfo
            {
                FileName = manifest.Command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
                return new ToolResult(false, "No se pudo iniciar el proceso del plugin.");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var outputTask = proc.StandardOutput.ReadToEndAsync(cts.Token);
            var errorTask = proc.StandardError.ReadToEndAsync(cts.Token);

            await proc.WaitForExitAsync(cts.Token);
            var output = await outputTask;
            var error = await errorTask;
            var text = string.IsNullOrWhiteSpace(output) ? error : output;

            if (string.IsNullOrWhiteSpace(text))
                text = "Plugin ejecutado correctamente.";

            if (text.Length > 3000)
                text = text[..3000] + "\n[...truncado]";

            return new ToolResult(proc.ExitCode == 0, text);
        }
        catch (OperationCanceledException)
        {
            return new ToolResult(false, "Plugin cancelado por timeout (30s).");
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Error al ejecutar plugin: {ex.Message}");
        }
    }

    private static ToolParameterSchema BuildSchema(PluginToolManifest m)
    {
        var schema = new ToolParameterSchema { Properties = new(), Required = new() };
        foreach (var (k, desc) in m.Parameters)
        {
            schema.Properties[k] = new ToolParameterProperty
            {
                Type = "string",
                Description = string.IsNullOrWhiteSpace(desc) ? $"Parámetro '{k}'" : desc
            };
            schema.Required.Add(k);
        }

        return schema;
    }

    private static string Escape(string value) => value.Replace("\"", "\\\"");
}
