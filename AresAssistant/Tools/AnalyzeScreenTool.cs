using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Windows.Forms;
using AresAssistant.Config;
using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public class AnalyzeScreenTool(OllamaClient client, ConfigManager configManager) : ITool
{
    public string Name => "analyze_screen";
    public string Description => "Captura la pantalla y la analiza con un modelo local multimodal de Ollama.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["question"] = new() { Type = "string", Description = "Qué quieres que analice en la captura" }
        },
        Required = new()
    };

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var question = args.TryGetValue("question", out var q) && !string.IsNullOrWhiteSpace(q?.ToString())
            ? q!.ToString()
            : "Describe lo que ves en la captura y destaca cualquier error importante.";

        Bitmap? capturedBmp = null;
        try
        {
            var bounds = SystemInformation.VirtualScreen;
            capturedBmp = new Bitmap(bounds.Width, bounds.Height);
            using var g = Graphics.FromImage(capturedBmp);
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        }
        catch (Exception ex)
        {
            capturedBmp?.Dispose();
            return new ToolResult(false, $"No se pudo capturar pantalla: {ex.Message}");
        }

        var imagesBase64 = BuildVisionImagesBase64(capturedBmp!);
        var windowHints = BuildWindowHints();
        capturedBmp?.Dispose();

        var cfg = configManager.Config;
        var installed = await client.GetInstalledModelsAsync();
        var candidates = BuildVisionCandidates(cfg, installed);
        if (candidates.Count == 0)
        {
            return new ToolResult(false,
                "No encontré modelos multimodales instalados para visión. Instala uno local (por ejemplo moondream:latest o llava:7b) y vuelve a intentar.");
        }

        var attemptPrompts = new[]
        {
            BuildAttemptPrompt(question, windowHints, allowInference: false),
            BuildAttemptPrompt(question, windowHints, allowInference: true)
        };

        foreach (var model in candidates)
        {
            for (int attempt = 0; attempt < attemptPrompts.Length; attempt++)
            {
                try
                {
                    var messages = new List<OllamaMessage>
                    {
                        new("system", "Eres un analista visual local. Responde SOLO en español de España, sin mezclar inglés. No uses frases en inglés. Salida breve: máximo 8 bullets y máximo 900 caracteres. Evita listas enormes de procesos internos del sistema."),
                        new("user", attemptPrompts[attempt])
                        {
                            Images = imagesBase64
                        }
                    };

                    var resp = await client.ChatAsync(messages, new List<ToolDefinition>(), model, keepAlive: "5m");
                    if (!string.IsNullOrWhiteSpace(resp.Error))
                        continue;

                    var text = resp.Message?.Content?.Trim();
                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    // Some text-only models return a polite refusal instead of image understanding.
                    if (LooksLikeVisionRefusal(text))
                        continue;

                    // Retry once with a stronger prompt if the answer is too generic.
                    if (LooksLikeLowConfidenceGeneric(text) && attempt < attemptPrompts.Length - 1)
                        continue;

                    text = await ForceSpanishOutputAsync(text, model);

                    return new ToolResult(true, $"[modelo: {model}]\n{CompactVisionResponse(text)}");
                }
                catch
                {
                    // Try next attempt / next fallback model.
                }
            }
        }

        return new ToolResult(false, "No se pudo analizar la captura con los modelos locales disponibles. Revisa que haya un modelo multimodal instalado (por ejemplo moondream:latest o llava:7b).");
    }

    private static List<string> BuildVisionCandidates(AppConfig cfg, List<string> installed)
    {
        var candidates = new List<string>();

        void Add(string? model)
        {
            if (string.IsNullOrWhiteSpace(model)) return;
            if (!candidates.Contains(model, StringComparer.OrdinalIgnoreCase))
                candidates.Add(model.Trim());
        }

        Add(cfg.MultiModelVisionModel);
        foreach (var m in (cfg.MultiModelFallbacks ?? string.Empty)
                     .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Add(m);
        }
        Add(cfg.OllamaModel);

        if (installed.Count > 0)
        {
            candidates = candidates
                .Where(c => installed.Any(i => i.Equals(c, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // Append any other installed vision model candidates not listed in settings.
            foreach (var model in installed.Where(ModelRouter.IsLikelyVisionModel))
                Add(model);

            candidates = candidates
                .Where(c => installed.Any(i => i.Equals(c, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        // Final guarantee: only likely vision models.
        return candidates.Where(ModelRouter.IsLikelyVisionModel).ToList();
    }

    private static bool LooksLikeVisionRefusal(string text)
    {
        var t = text.ToLowerInvariant();
        return (t.Contains("no puedo ver") ||
                t.Contains("no puedo analizar imágenes") ||
                t.Contains("cannot view images") ||
                t.Contains("não consigo") ||
                t.Contains("imagem não está disponível") ||
                t.Contains("image is not available"))
               && (t.Contains("imagen") || t.Contains("image") || t.Contains("adjunto") || t.Contains("archivo"));
    }

    private static bool LooksLikeLowConfidenceGeneric(string text)
    {
        var t = text.ToLowerInvariant();
        return t.Contains("no hay información suficiente") ||
               t.Contains("no se puede identificar") ||
               t.Contains("no se pueden identificar") ||
               t.Contains("cannot identify") ||
               t.Contains("not enough information") ||
               t.Contains("não há informação suficiente");
    }

    private static string BuildAttemptPrompt(string question, string windowHints, bool allowInference)
    {
        var basePrompt =
            $"Pregunta del usuario: {question}\n\n" +
            "Analiza las imágenes adjuntas (pantalla completa y recortes).\n" +
            "Idioma obligatorio de salida: español de España. No mezcles inglés.\n" +
            "Devuelve:\n" +
            "1) Apps/páginas detectadas (solo relevantes, máximo 5)\n" +
            "2) Qué está pasando en las ventanas principales\n" +
            "3) Errores o anomalías visibles\n" +
            "4) Evidencia textual breve (si hay)\n\n" +
            (string.IsNullOrWhiteSpace(windowHints)
                ? ""
                : $"Contexto de ventanas abiertas (apoyo):\n{windowHints}\n\n");

        if (!allowInference)
            return basePrompt + "Si no tienes certeza, dilo; no inventes.";

        return basePrompt + "Haz mejor esfuerzo para identificar apps por iconos/estructura. Si no hay certeza total, marca como 'probable'. Limita tu respuesta a 8 bullets.";
    }

    private async Task<string> ForceSpanishOutputAsync(string text, string model)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        if (!LooksLikelyEnglishMixed(text))
            return text;

        try
        {
            var rewrite = await client.ChatAsync(
                new List<OllamaMessage>
                {
                    new("system", "Convierte el texto recibido a español de España. Mantén el formato de viñetas y el contenido técnico, sin añadir información nueva. Devuelve solo el texto final en español."),
                    new("user", text)
                },
                new List<ToolDefinition>(),
                model,
                keepAlive: "5m");

            var rewritten = rewrite.Message?.Content?.Trim();
            if (!string.IsNullOrWhiteSpace(rewritten))
                return rewritten;
        }
        catch
        {
            // If rewrite fails, keep original response.
        }

        return text;
    }

    private static bool LooksLikelyEnglishMixed(string text)
    {
        var t = text.ToLowerInvariant();
        var markers = new[]
        {
            " is ", " are ", " the ", " and ", " with ", " open ", " appears ",
            "display", "shows", "window", "likely", "contains", "including"
        };

        var hits = markers.Count(m => t.Contains(m, StringComparison.Ordinal));
        return hits >= 3;
    }

    private static string CompactVisionResponse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var normalized = text.Replace("\r\n", "\n").Trim();

        // Preserve full response so the scrollable modal can display the entire analysis.
        // Keep only a high hard ceiling as a safety net for pathological outputs.
        const int hardCap = 12000;
        return normalized.Length <= hardCap
            ? normalized
            : normalized[..hardCap] + "\n\n[Respuesta recortada por límite de seguridad]";
    }

    private static string BuildWindowHints()
    {
        try
        {
            var hints = Process.GetProcesses()
                .Where(p => !string.IsNullOrWhiteSpace(p.MainWindowTitle))
                .Select(p => $"- {p.ProcessName}: {p.MainWindowTitle}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToList();

            return string.Join(Environment.NewLine, hints);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static List<string> BuildVisionImagesBase64(Bitmap bmp)
    {
        var images = new List<string>
        {
            EncodeJpegBase64(bmp, maxSide: 2200, quality: 92L)
        };

        // Extra crops improve OCR/details for small UI text and app identification.
        var w = bmp.Width;
        var h = bmp.Height;
        var crop1 = new Rectangle(0, 0, Math.Max(1, (int)(w * 0.65)), Math.Max(1, (int)(h * 0.65))); // top-left
        var crop2 = new Rectangle(Math.Max(0, (int)(w * 0.35)), 0, Math.Max(1, (int)(w * 0.65)), Math.Max(1, (int)(h * 0.65))); // top-right

        using (var b1 = bmp.Clone(crop1, bmp.PixelFormat))
            images.Add(EncodeJpegBase64(b1, maxSide: 1800, quality: 90L));

        using (var b2 = bmp.Clone(crop2, bmp.PixelFormat))
            images.Add(EncodeJpegBase64(b2, maxSide: 1800, quality: 90L));

        return images;
    }

    private static string EncodeJpegBase64(Bitmap bmp, int maxSide, long quality)
    {
        var scale = Math.Min(1.0, maxSide / (double)Math.Max(bmp.Width, bmp.Height));
        var targetW = Math.Max(1, (int)Math.Round(bmp.Width * scale));
        var targetH = Math.Max(1, (int)Math.Round(bmp.Height * scale));

        using var resized = new Bitmap(targetW, targetH);
        using (var g = Graphics.FromImage(resized))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(bmp, 0, 0, targetW, targetH);
        }

        using var ms = new MemoryStream();
        var encoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(e => e.FormatID == ImageFormat.Jpeg.Guid);
        if (encoder == null)
        {
            resized.Save(ms, ImageFormat.Jpeg);
        }
        else
        {
            using var encParams = new EncoderParameters(1);
            encParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            resized.Save(ms, encoder, encParams);
        }

        return Convert.ToBase64String(ms.ToArray());
    }
}
