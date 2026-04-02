using System.Diagnostics;
using AresAssistant.Config;
using AresAssistant.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public class ModelBenchmarkTool(OllamaClient ollamaClient, ConfigManager configManager) : ITool
{
    public string Name => "model_benchmark";
    public string Description => "Ejecuta benchmark local de modelos por tarea y devuelve latencia y resultado básico.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["models"] = new() { Type = "string", Description = "Lista de modelos separada por comas. Opcional." },
            ["task"] = new() { Type = "string", Description = "Tipo: coding, reasoning o vision", Default = "reasoning" }
        },
        Required = new()
    };

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var cfg = configManager.Config;
        var task = args.TryGetValue("task", out var t) ? t.ToString().Trim().ToLowerInvariant() : "reasoning";
        var installed = await ollamaClient.GetInstalledModelsAsync().ConfigureAwait(false);

        var requested = args.TryGetValue("models", out var m)
            ? m.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : new List<string>();

        var models = requested.Count > 0
            ? requested.Where(r => installed.Any(i => i.Equals(r, StringComparison.OrdinalIgnoreCase))).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            : ModelRouter.BuildCandidates(task switch
            {
                "coding" => "tengo un error de compilación en c#",
                "vision" => "analiza esta captura",
                _ => "ayúdame a planificar mi día"
            }, cfg, installed).Take(3).ToList();

        if (models.Count == 0)
            return new ToolResult(false, "No hay modelos instalados para benchmark.");

        var (numCtx, numThread, _, numPredict, numBatch) = cfg.GetPerformanceParams();
        var keepAlive = cfg.ModelKeepAliveMinutes > 0 ? $"{cfg.ModelKeepAliveMinutes}m" : "-1";

        var prompt = task switch
        {
            "coding" => "Explica en 2 frases cómo resolver un NullReferenceException en C#.",
            "vision" => "Describe en una frase qué harías para depurar una captura con error visual.",
            _ => "Resume en 2 frases cómo priorizar tareas hoy."
        };

        var rows = new List<object>();
        foreach (var model in models)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var resp = await ollamaClient.ChatAsync(
                    new List<OllamaMessage> { new("user", prompt) },
                    new List<ToolDefinition>(),
                    model,
                    numCtx,
                    numThread,
                    Math.Min(numPredict, 128),
                    numBatch,
                    keepAlive).ConfigureAwait(false);

                sw.Stop();
                var text = (resp.Message.Content ?? string.Empty).Trim();
                rows.Add(new
                {
                    model,
                    ok = string.IsNullOrWhiteSpace(resp.Error),
                    latency_ms = sw.ElapsedMilliseconds,
                    output_preview = text.Length > 120 ? text[..120] + "..." : text
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                rows.Add(new
                {
                    model,
                    ok = false,
                    latency_ms = sw.ElapsedMilliseconds,
                    output_preview = "error: " + ex.Message
                });
            }
        }

        return new ToolResult(true, JsonConvert.SerializeObject(new
        {
            task,
            tested_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            results = rows
        }, Formatting.Indented));
    }
}
