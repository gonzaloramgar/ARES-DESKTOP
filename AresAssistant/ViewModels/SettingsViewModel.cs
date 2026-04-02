using System.Collections.ObjectModel;
using AresAssistant.Config;
using AresAssistant.Core;

namespace AresAssistant.ViewModels;

/// <summary>
/// ViewModel de la pantalla de ajustes.
/// Expone todas las propiedades configurables (tema, modelo, voz, rendimiento…)
/// y métodos para guardar, verificar Ollama y detectar hardware.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly ConfigManager _configManager;
    private readonly OllamaClient _ollamaClient;

    private string _accentColor;
    private double _overlayOpacity;
    private string _fontSize;
    private string _overlayPosition;
    private string _ollamaModel;
    private bool _multiModelEnabled;
    private string _multiModelCodingModel;
    private string _multiModelReasoningModel;
    private string _multiModelVisionModel;
    private string _multiModelFallbacks;
    private string _assistantName;
    private string _personality;
    private string _contextProfile;
    private string _responseLength;
    private string _showHideHotkey;
    private string _toggleModeHotkey;
    private bool _launchWithWindows;
    private bool _saveChatHistory;
    private bool _closeToTray;
    private bool _confirmationAlertsEnabled;
    private bool _autonomousMode;
    private bool _processContextEnabled;
    private bool _scheduledAutomationsEnabled;
    private bool _clipboardSmartEnabled;
    private bool _reliabilityTelemetryEnabled;
    private bool _autoApplyContextProfilePresets;
    private bool _runtimeAdaptiveRouting;
    private bool _securityPolicyEnabled;
    private bool _pluginToolsEnabled;
    private bool _localApiEnabled;
    private int _localApiPort;
    private bool _widgetWeatherEnabled;
    private bool _widgetWorldClockEnabled;
    private bool _widgetSystemLiveEnabled;
    private bool _widgetTasksEnabled;
    private bool _widgetProductivityEnabled;
    private int _modelKeepAliveMinutes;
    private bool _voiceEnabled;
    private bool _ollamaAvailable;
    private string _ollamaStatus = "";
    private string _performanceMode;
    private string _hwCpuInfo = "";
    private string _hwRamInfo = "";
    private string _hwRecommendation = "";
    private string _ttsEngineStatus = "";
    private System.Windows.Visibility _piperButtonVisible = System.Windows.Visibility.Collapsed;
    private bool _canDownloadPiper = true;
    private float _ttsVolume;
    private string _ttsVoiceGender;

    public ObservableCollection<string> AvailableModels { get; } = new();

    public string AccentColor { get => _accentColor; set { SetField(ref _accentColor, value); } }
    public double OverlayOpacity { get => _overlayOpacity; set => SetField(ref _overlayOpacity, value); }
    public string FontSize { get => _fontSize; set => SetField(ref _fontSize, value); }
    public string OverlayPosition { get => _overlayPosition; set => SetField(ref _overlayPosition, value); }
    public string OllamaModel { get => _ollamaModel; set => SetField(ref _ollamaModel, value); }
    public bool MultiModelEnabled { get => _multiModelEnabled; set => SetField(ref _multiModelEnabled, value); }
    public string MultiModelCodingModel { get => _multiModelCodingModel; set => SetField(ref _multiModelCodingModel, value); }
    public string MultiModelReasoningModel { get => _multiModelReasoningModel; set => SetField(ref _multiModelReasoningModel, value); }
    public string MultiModelVisionModel { get => _multiModelVisionModel; set => SetField(ref _multiModelVisionModel, value); }
    public string MultiModelFallbacks { get => _multiModelFallbacks; set => SetField(ref _multiModelFallbacks, value); }
    public string AssistantName { get => _assistantName; set => SetField(ref _assistantName, value); }
    public string Personality { get => _personality; set => SetField(ref _personality, value); }
    public string ContextProfile { get => _contextProfile; set => SetField(ref _contextProfile, value); }
    public string ResponseLength { get => _responseLength; set => SetField(ref _responseLength, value); }
    public string ShowHideHotkey { get => _showHideHotkey; set => SetField(ref _showHideHotkey, value); }
    public string ToggleModeHotkey { get => _toggleModeHotkey; set => SetField(ref _toggleModeHotkey, value); }
    public bool LaunchWithWindows { get => _launchWithWindows; set => SetField(ref _launchWithWindows, value); }
    public bool SaveChatHistory { get => _saveChatHistory; set => SetField(ref _saveChatHistory, value); }
    public bool CloseToTray { get => _closeToTray; set => SetField(ref _closeToTray, value); }
    public bool ConfirmationAlertsEnabled { get => _confirmationAlertsEnabled; set => SetField(ref _confirmationAlertsEnabled, value); }
    public bool AutonomousMode { get => _autonomousMode; set => SetField(ref _autonomousMode, value); }
    public bool ProcessContextEnabled { get => _processContextEnabled; set => SetField(ref _processContextEnabled, value); }
    public bool ScheduledAutomationsEnabled { get => _scheduledAutomationsEnabled; set => SetField(ref _scheduledAutomationsEnabled, value); }
    public bool ClipboardSmartEnabled { get => _clipboardSmartEnabled; set => SetField(ref _clipboardSmartEnabled, value); }
    public bool ReliabilityTelemetryEnabled { get => _reliabilityTelemetryEnabled; set => SetField(ref _reliabilityTelemetryEnabled, value); }
    public bool AutoApplyContextProfilePresets { get => _autoApplyContextProfilePresets; set => SetField(ref _autoApplyContextProfilePresets, value); }
    public bool RuntimeAdaptiveRouting { get => _runtimeAdaptiveRouting; set => SetField(ref _runtimeAdaptiveRouting, value); }
    public bool SecurityPolicyEnabled { get => _securityPolicyEnabled; set => SetField(ref _securityPolicyEnabled, value); }
    public bool PluginToolsEnabled { get => _pluginToolsEnabled; set => SetField(ref _pluginToolsEnabled, value); }
    public bool LocalApiEnabled { get => _localApiEnabled; set => SetField(ref _localApiEnabled, value); }
    public int LocalApiPort { get => _localApiPort; set => SetField(ref _localApiPort, Math.Clamp(value, 1024, 65535)); }
    public bool WidgetWeatherEnabled { get => _widgetWeatherEnabled; set => SetField(ref _widgetWeatherEnabled, value); }
    public bool WidgetWorldClockEnabled { get => _widgetWorldClockEnabled; set => SetField(ref _widgetWorldClockEnabled, value); }
    public bool WidgetSystemLiveEnabled { get => _widgetSystemLiveEnabled; set => SetField(ref _widgetSystemLiveEnabled, value); }
    public bool WidgetTasksEnabled { get => _widgetTasksEnabled; set => SetField(ref _widgetTasksEnabled, value); }
    public bool WidgetProductivityEnabled { get => _widgetProductivityEnabled; set => SetField(ref _widgetProductivityEnabled, value); }
    public int ModelKeepAliveMinutes { get => _modelKeepAliveMinutes; set => SetField(ref _modelKeepAliveMinutes, value); }
    public bool VoiceEnabled { get => _voiceEnabled; set => SetField(ref _voiceEnabled, value); }
    public bool OllamaAvailable { get => _ollamaAvailable; set => SetField(ref _ollamaAvailable, value); }
    public string OllamaStatus { get => _ollamaStatus; set => SetField(ref _ollamaStatus, value); }
    public string PerformanceMode { get => _performanceMode; set => SetField(ref _performanceMode, value); }
    public string HwCpuInfo { get => _hwCpuInfo; set => SetField(ref _hwCpuInfo, value); }
    public string HwRamInfo { get => _hwRamInfo; set => SetField(ref _hwRamInfo, value); }
    public string HwRecommendation { get => _hwRecommendation; set => SetField(ref _hwRecommendation, value); }
    public string TtsEngineStatus { get => _ttsEngineStatus; set => SetField(ref _ttsEngineStatus, value); }
    public System.Windows.Visibility PiperButtonVisible { get => _piperButtonVisible; set => SetField(ref _piperButtonVisible, value); }
    public bool CanDownloadPiper { get => _canDownloadPiper; set => SetField(ref _canDownloadPiper, value); }
    public float TtsVolume
    {
        get => _ttsVolume;
        set
        {
            SetField(ref _ttsVolume, value);
            // Apply live so the user hears the change immediately with the test button
            if (Views.MainWindow.SpeechEngine is { } s) s.Volume = value;
        }
    }

    public string TtsVoiceGender
    {
        get => _ttsVoiceGender;
        set
        {
            SetField(ref _ttsVoiceGender, value);
            if (Views.MainWindow.SpeechEngine is { } s) s.VoiceGender = value;
        }
    }

    public SettingsViewModel(ConfigManager configManager, OllamaClient ollamaClient)
    {
        _configManager = configManager;
        _ollamaClient = ollamaClient;

        var cfg = configManager.Config;
        _accentColor = cfg.AccentColor;
        _overlayOpacity = cfg.OverlayOpacity;
        _fontSize = cfg.FontSize;
        _overlayPosition = cfg.OverlayPosition;
        _ollamaModel = cfg.OllamaModel;
        _multiModelEnabled = cfg.MultiModelEnabled;
        _multiModelCodingModel = cfg.MultiModelCodingModel;
        _multiModelReasoningModel = cfg.MultiModelReasoningModel;
        _multiModelVisionModel = cfg.MultiModelVisionModel;
        _multiModelFallbacks = cfg.MultiModelFallbacks;
        _assistantName = cfg.AssistantName;
        _personality = cfg.Personality;
        _contextProfile = cfg.ContextProfile;
        _responseLength = cfg.ResponseLength;
        _showHideHotkey = cfg.ShowHideHotkey;
        _toggleModeHotkey = cfg.ToggleModeHotkey;
        // Always read from actual registry state so the checkbox reflects reality
        _launchWithWindows = StartupManager.IsEnabled;
        _saveChatHistory = cfg.SaveChatHistory;
        _closeToTray = cfg.CloseToTray;
        _confirmationAlertsEnabled = cfg.ConfirmationAlertsEnabled;
        _autonomousMode = cfg.AutonomousMode;
        _processContextEnabled = cfg.ProcessContextEnabled;
        _scheduledAutomationsEnabled = cfg.ScheduledAutomationsEnabled;
        _clipboardSmartEnabled = cfg.ClipboardSmartEnabled;
        _reliabilityTelemetryEnabled = cfg.ReliabilityTelemetryEnabled;
        _autoApplyContextProfilePresets = cfg.AutoApplyContextProfilePresets;
        _runtimeAdaptiveRouting = cfg.RuntimeAdaptiveRouting;
        _securityPolicyEnabled = cfg.SecurityPolicyEnabled;
        _pluginToolsEnabled = cfg.PluginToolsEnabled;
        _localApiEnabled = cfg.LocalApiEnabled;
        _localApiPort = cfg.LocalApiPort;
        _widgetWeatherEnabled = cfg.WidgetWeatherEnabled;
        _widgetWorldClockEnabled = cfg.WidgetWorldClockEnabled;
        _widgetSystemLiveEnabled = cfg.WidgetSystemLiveEnabled;
        _widgetTasksEnabled = cfg.WidgetTasksEnabled;
        _widgetProductivityEnabled = cfg.WidgetProductivityEnabled;
        _modelKeepAliveMinutes = cfg.ModelKeepAliveMinutes;
        _voiceEnabled = cfg.VoiceEnabled;
        _ttsVolume = cfg.TtsVolume;
        _ttsVoiceGender = cfg.TtsVoiceGender;
        _performanceMode = cfg.PerformanceMode;
    }

    public AppConfig BuildConfig() => new()
    {
        AccentColor = AccentColor,
        OverlayOpacity = OverlayOpacity,
        FontSize = FontSize,
        OverlayPosition = OverlayPosition,
        OllamaModel = OllamaModel,
        MultiModelEnabled = MultiModelEnabled,
        MultiModelCodingModel = MultiModelCodingModel,
        MultiModelReasoningModel = MultiModelReasoningModel,
        MultiModelVisionModel = MultiModelVisionModel,
        MultiModelFallbacks = MultiModelFallbacks,
        AssistantName = AssistantName,
        Personality = Personality,
        ContextProfile = ContextProfile,
        ResponseLength = ResponseLength,
        ShowHideHotkey = ShowHideHotkey,
        ToggleModeHotkey = ToggleModeHotkey,
        LaunchWithWindows = LaunchWithWindows,
        SaveChatHistory = SaveChatHistory,
        CloseToTray = CloseToTray,
        ConfirmationAlertsEnabled = ConfirmationAlertsEnabled,
        AutonomousMode = AutonomousMode,
        ProcessContextEnabled = ProcessContextEnabled,
        ScheduledAutomationsEnabled = ScheduledAutomationsEnabled,
        ClipboardSmartEnabled = ClipboardSmartEnabled,
        ReliabilityTelemetryEnabled = ReliabilityTelemetryEnabled,
        AutoApplyContextProfilePresets = AutoApplyContextProfilePresets,
        RuntimeAdaptiveRouting = RuntimeAdaptiveRouting,
        SecurityPolicyEnabled = SecurityPolicyEnabled,
        PluginToolsEnabled = PluginToolsEnabled,
        LocalApiEnabled = LocalApiEnabled,
        LocalApiPort = LocalApiPort,
        WidgetWeatherEnabled = WidgetWeatherEnabled,
        WidgetWorldClockEnabled = WidgetWorldClockEnabled,
        WidgetSystemLiveEnabled = WidgetSystemLiveEnabled,
        WidgetTasksEnabled = WidgetTasksEnabled,
        WidgetProductivityEnabled = WidgetProductivityEnabled,
        ModelKeepAliveMinutes = ModelKeepAliveMinutes,
        VoiceEnabled = VoiceEnabled,
        TtsVolume = TtsVolume,
        TtsVoiceGender = TtsVoiceGender,
        PerformanceMode = PerformanceMode,
        SetupCompleted = _configManager.Config.SetupCompleted
    };

    public void Save()
    {
        var config = BuildConfig();
        config = ApplyContextProfilePresets(config);
        _configManager.Save(config);
        ThemeEngine.Apply(config);
        StartupManager.SetEnabled(LaunchWithWindows);

        // Sync speech engine
        if (Views.MainWindow.SpeechEngine is { } speech)
        {
            speech.Enabled = config.VoiceEnabled;
            speech.Volume = config.TtsVolume;
            speech.VoiceGender = config.TtsVoiceGender;
        }

        if (Views.MainWindow.PermissionManager is { } perms)
            perms.AutoApproveConfirmations = config.AutonomousMode;
    }

    private static AppConfig ApplyContextProfilePresets(AppConfig config)
    {
        if (!config.AutoApplyContextProfilePresets)
            return config;

        var profile = (config.ContextProfile ?? "").Trim().ToLowerInvariant();
        return profile switch
        {
            "trabajo" => config with
            {
                Personality = "tecnico",
                ResponseLength = "conciso",
                AutonomousMode = true,
                ScheduledAutomationsEnabled = true,
                WidgetProductivityEnabled = true,
                WidgetTasksEnabled = true
            },
            "estudio" => config with
            {
                Personality = "formal",
                ResponseLength = "detallado",
                AutonomousMode = false,
                ScheduledAutomationsEnabled = true,
                WidgetProductivityEnabled = true,
                WidgetTasksEnabled = true
            },
            "gaming" => config with
            {
                Personality = "casual",
                ResponseLength = "conciso",
                AutonomousMode = false,
                ScheduledAutomationsEnabled = false,
                WidgetProductivityEnabled = false,
                WidgetTasksEnabled = false
            },
            _ => config
        };
    }

    public async Task CheckOllamaAsync()
    {
        OllamaStatus = "Verificando Ollama...";
        OllamaAvailable = await _ollamaClient.IsAvailableAsync();
        OllamaStatus = OllamaAvailable ? "Ollama conectado ✓" : "Ollama no disponible ✗";

        if (OllamaAvailable)
        {
            var models = await _ollamaClient.GetInstalledModelsAsync();
            AvailableModels.Clear();
            foreach (var m in models)
                AvailableModels.Add(m);
        }
    }

    public void RefreshTtsStatus()
    {
        var speech = Views.MainWindow.SpeechEngine;
        if (speech == null) { TtsEngineStatus = "Voz no disponible"; return; }

        if (speech.PiperDownloading)
        {
            TtsEngineStatus = "Descargando Piper...";
            PiperButtonVisible = System.Windows.Visibility.Visible;
            CanDownloadPiper = false;
        }
        else if (speech.PiperReady)
        {
            TtsEngineStatus = "Motor: Piper neural offline ✓";
            PiperButtonVisible = System.Windows.Visibility.Collapsed;
            CanDownloadPiper = false;
        }
        else
        {
            TtsEngineStatus = "Motor: Edge online / Local";
            PiperButtonVisible = System.Windows.Visibility.Visible;
            CanDownloadPiper = true;
        }
    }

    public async Task DetectHardwareAsync()
    {
        try
        {
            var hw = await Task.Run(HardwareDetector.Detect);
            HwCpuInfo = $"CPU: {hw.CpuName} ({hw.CpuCores} hilos)";
            HwRamInfo = $"RAM: {hw.TotalRamGb:F1} GB";
            HwRecommendation = hw.RecommendedMode == "avanzado"
                ? "⮞ Recomendado: Avanzado"
                : "⮞ Recomendado: Ligero";
        }
        catch
        {
            HwCpuInfo = "CPU: no detectado";
            HwRamInfo = "RAM: no detectada";
            HwRecommendation = "⮞ Recomendado: Ligero";
        }
    }

    public async Task<string> BuildMultiModelRoutingPreviewAsync()
    {
        var cfg = BuildConfig();
        var installed = await _ollamaClient.GetInstalledModelsAsync();

        string RouteStatus(string title, string prompt)
        {
            var cands = ModelRouter.BuildCandidates(prompt, cfg, installed);
            if (cands.Count == 0)
                return $"{title}: sin candidatos";

            var primary = cands[0];
            var primaryInstalled = installed.Any(i => i.Equals(primary, StringComparison.OrdinalIgnoreCase));
            var chain = string.Join(" -> ", cands);
            return $"{title}: {chain}{Environment.NewLine}  Primario: {primary} {(primaryInstalled ? "✓" : "✗")}";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Diagnóstico de routing multimodelo (solo lectura)");
        sb.AppendLine("Este botón NO ejecuta prompts reales ni cambia configuración.");
        sb.AppendLine($"Multimodelo activo: {(cfg.MultiModelEnabled ? "sí" : "no")}");
        sb.AppendLine($"Modelos instalados: {(installed.Count == 0 ? "ninguno" : string.Join(", ", installed))}");
        var missing = ModelRouter.GetMissingPreferredModels(cfg, installed);
        if (missing.Count > 0)
        {
            sb.AppendLine($"⚠ Faltan modelos preferidos: {string.Join(", ", missing)}");
            sb.AppendLine("   Puedes instalarlos directamente desde este mismo diagnóstico.");
        }
        else
        {
            sb.AppendLine("✓ Modelos preferidos disponibles.");
        }

        if (!cfg.MultiModelEnabled)
        {
            sb.AppendLine();
            sb.AppendLine("ℹ Con multimodelo desactivado, ARES usará OllamaModel + fallbacks configurados.");
        }

        sb.AppendLine();
        sb.AppendLine(RouteStatus("Coding", "Tengo un error de compilación en C# y un stack trace"));
        sb.AppendLine();
        sb.AppendLine(RouteStatus("Reasoning", "Ayúdame a planificar las tareas de hoy"));
        sb.AppendLine();
        sb.AppendLine(RouteStatus("Visión", "¿Qué hay en esta captura de pantalla?"));

        return sb.ToString();
    }

    public async Task<List<string>> GetMissingPreferredModelsAsync()
    {
        var cfg = BuildConfig();
        var installed = await _ollamaClient.GetInstalledModelsAsync();
        return ModelRouter.GetMissingPreferredModels(cfg, installed)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
