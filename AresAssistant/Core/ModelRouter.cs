using AresAssistant.Config;

namespace AresAssistant.Core;

public static class ModelRouter
{
    public static List<string> BuildCandidates(string userMessage, AppConfig config, List<string> installedModels)
    {
        var candidates = new List<string>();

        void Add(string? model)
        {
            if (string.IsNullOrWhiteSpace(model)) return;
            if (!candidates.Contains(model, StringComparer.OrdinalIgnoreCase))
                candidates.Add(model.Trim());
        }

        if (config.MultiModelEnabled)
        {
            var intent = InferIntent(userMessage);
            switch (intent)
            {
                case ModelIntent.Vision:
                    Add(config.MultiModelVisionModel);
                    break;
                case ModelIntent.Coding:
                    Add(config.MultiModelCodingModel);
                    break;
                default:
                    Add(config.MultiModelReasoningModel);
                    break;
            }
        }

        Add(config.OllamaModel);

        foreach (var m in SplitCsv(config.MultiModelFallbacks))
            Add(m);

        if (installedModels.Count > 0)
        {
            candidates = candidates
                .Where(c => installedModels.Any(i => i.Equals(c, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (candidates.Count == 0)
            {
                // Fallback defensivo: primer modelo instalado.
                Add(installedModels[0]);
            }
        }

        if (config.RuntimeAdaptiveRouting)
            candidates = RuntimeModelAdvisor.ReorderCandidates(candidates);

        return candidates;
    }

    public static bool IsLikelyVisionModel(string modelName)
    {
        var m = (modelName ?? string.Empty).ToLowerInvariant();
        return m.Contains("llava") ||
               m.Contains("bakllava") ||
               m.Contains("moondream") ||
               m.Contains("vision") ||
               m.Contains("minicpm-v") ||
               m.Contains("qwen2.5vl") ||
               m.Contains("qwen-vl");
    }

    public static List<string> GetMissingPreferredModels(AppConfig config, List<string> installedModels)
    {
        var preferred = new List<string>
        {
            config.MultiModelReasoningModel,
            config.MultiModelCodingModel,
            config.MultiModelVisionModel
        }
        .Where(m => !string.IsNullOrWhiteSpace(m))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

        return preferred
            .Where(p => !installedModels.Any(i => i.Equals(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static IEnumerable<string> SplitCsv(string csv)
        => (csv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x));

    private static ModelIntent InferIntent(string text)
    {
        text = (text ?? string.Empty).ToLowerInvariant();

        if (text.Contains("captura") ||
            text.Contains("pantalla") ||
            text.Contains("imagen") ||
            text.Contains("imágenes") ||
            text.Contains("foto") ||
            text.Contains("vision") ||
            text.Contains("visión") ||
            text.Contains("analiza") ||
            text.Contains("analizar"))
            return ModelIntent.Vision;

        if (text.Contains("código") || text.Contains("codigo") || text.Contains("programa") || text.Contains("error") || text.Contains("stack") || text.Contains("debug") || text.Contains("compilar"))
            return ModelIntent.Coding;

        return ModelIntent.Reasoning;
    }

    private enum ModelIntent
    {
        Reasoning,
        Coding,
        Vision
    }
}
