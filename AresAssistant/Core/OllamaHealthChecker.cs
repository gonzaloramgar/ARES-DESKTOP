using AresAssistant.Config;

namespace AresAssistant.Core;

public sealed class OllamaHealthChecker
{
    public async Task<OllamaHealthReport> CheckAsync(OllamaClient client, AppConfig config, CancellationToken ct = default)
    {
        var report = new OllamaHealthReport
        {
            CheckedAtLocal = DateTime.Now,
            IsInstalled = OllamaClient.IsInstalled()
        };

        if (!report.IsInstalled)
        {
            report.Status = OllamaHealthStatus.NotInstalled;
            report.Issues.Add("Ollama no está instalado.");
            report.Actions.Add("Instala Ollama desde el asistente inicial o desde https://ollama.com/download.");
            return report;
        }

        report.ApiAvailable = await client.IsAvailableAsync().ConfigureAwait(false);
        if (!report.ApiAvailable)
        {
            report.Status = OllamaHealthStatus.InstalledNotRunning;
            report.Issues.Add("Ollama está instalado pero la API local no responde.");
            report.Actions.Add("Abre Ollama o usa la reparación automática para iniciarlo.");
            return report;
        }

        report.InstalledModels = await client.GetInstalledModelsAsync().ConfigureAwait(false);
        var preferred = ModelRouter.GetMissingPreferredModels(config, report.InstalledModels)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        report.MissingPreferredModels = preferred;

        if (preferred.Count > 0)
        {
            report.Status = OllamaHealthStatus.ModelsMissing;
            report.Issues.Add($"Faltan modelos preferidos: {string.Join(", ", preferred)}.");
            report.Actions.Add("Pulsa 'Instalar faltantes' o 'Reparar todo IA'.");
            return report;
        }

        report.Status = OllamaHealthStatus.Healthy;
        report.Actions.Add("Estado correcto. No se requiere acción.");
        return report;
    }
}

public enum OllamaHealthStatus
{
    Healthy,
    NotInstalled,
    InstalledNotRunning,
    ModelsMissing
}

public sealed class OllamaHealthReport
{
    public DateTime CheckedAtLocal { get; init; }
    public OllamaHealthStatus Status { get; set; } = OllamaHealthStatus.Healthy;
    public bool IsInstalled { get; init; }
    public bool ApiAvailable { get; set; }
    public List<string> InstalledModels { get; set; } = new();
    public List<string> MissingPreferredModels { get; set; } = new();
    public List<string> Issues { get; } = new();
    public List<string> Actions { get; } = new();

    public string ToCompactText()
    {
        var statusText = Status switch
        {
            OllamaHealthStatus.Healthy => "SALUD IA: OK",
            OllamaHealthStatus.NotInstalled => "SALUD IA: OLLAMA NO INSTALADO",
            OllamaHealthStatus.InstalledNotRunning => "SALUD IA: OLLAMA NO DISPONIBLE",
            OllamaHealthStatus.ModelsMissing => "SALUD IA: MODELOS FALTANTES",
            _ => "SALUD IA: DESCONOCIDO"
        };

        var lines = new List<string>
        {
            statusText,
            $"- Ollama instalado: {(IsInstalled ? "sí" : "no")}",
            $"- API disponible: {(ApiAvailable ? "sí" : "no")}",
            $"- Modelos instalados: {(InstalledModels.Count == 0 ? "ninguno" : string.Join(", ", InstalledModels))}"
        };

        if (MissingPreferredModels.Count > 0)
            lines.Add($"- Modelos preferidos faltantes: {string.Join(", ", MissingPreferredModels)}");

        foreach (var issue in Issues)
            lines.Add($"- Issue: {issue}");
        foreach (var action in Actions)
            lines.Add($"- Acción: {action}");

        return string.Join(Environment.NewLine, lines);
    }
}
