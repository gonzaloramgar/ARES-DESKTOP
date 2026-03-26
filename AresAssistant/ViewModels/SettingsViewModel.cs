using System.Collections.ObjectModel;
using AresAssistant.Config;
using AresAssistant.Core;

namespace AresAssistant.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ConfigManager _configManager;
    private readonly OllamaClient _ollamaClient;

    private string _accentColor;
    private double _overlayOpacity;
    private string _fontSize;
    private string _overlayPosition;
    private string _ollamaModel;
    private string _assistantName;
    private string _personality;
    private string _responseLength;
    private string _showHideHotkey;
    private string _toggleModeHotkey;
    private bool _launchWithWindows;
    private bool _saveChatHistory;
    private bool _closeToTray;
    private bool _confirmationAlertsEnabled;
    private int _modelKeepAliveMinutes;
    private bool _ollamaAvailable;
    private string _ollamaStatus = "";
    private string _performanceMode;
    private string _hwCpuInfo = "";
    private string _hwRamInfo = "";
    private string _hwRecommendation = "";

    public ObservableCollection<string> AvailableModels { get; } = new();

    public string AccentColor { get => _accentColor; set { SetField(ref _accentColor, value); } }
    public double OverlayOpacity { get => _overlayOpacity; set => SetField(ref _overlayOpacity, value); }
    public string FontSize { get => _fontSize; set => SetField(ref _fontSize, value); }
    public string OverlayPosition { get => _overlayPosition; set => SetField(ref _overlayPosition, value); }
    public string OllamaModel { get => _ollamaModel; set => SetField(ref _ollamaModel, value); }
    public string AssistantName { get => _assistantName; set => SetField(ref _assistantName, value); }
    public string Personality { get => _personality; set => SetField(ref _personality, value); }
    public string ResponseLength { get => _responseLength; set => SetField(ref _responseLength, value); }
    public string ShowHideHotkey { get => _showHideHotkey; set => SetField(ref _showHideHotkey, value); }
    public string ToggleModeHotkey { get => _toggleModeHotkey; set => SetField(ref _toggleModeHotkey, value); }
    public bool LaunchWithWindows { get => _launchWithWindows; set => SetField(ref _launchWithWindows, value); }
    public bool SaveChatHistory { get => _saveChatHistory; set => SetField(ref _saveChatHistory, value); }
    public bool CloseToTray { get => _closeToTray; set => SetField(ref _closeToTray, value); }
    public bool ConfirmationAlertsEnabled { get => _confirmationAlertsEnabled; set => SetField(ref _confirmationAlertsEnabled, value); }
    public int ModelKeepAliveMinutes { get => _modelKeepAliveMinutes; set => SetField(ref _modelKeepAliveMinutes, value); }
    public bool OllamaAvailable { get => _ollamaAvailable; set => SetField(ref _ollamaAvailable, value); }
    public string OllamaStatus { get => _ollamaStatus; set => SetField(ref _ollamaStatus, value); }
    public string PerformanceMode { get => _performanceMode; set => SetField(ref _performanceMode, value); }
    public string HwCpuInfo { get => _hwCpuInfo; set => SetField(ref _hwCpuInfo, value); }
    public string HwRamInfo { get => _hwRamInfo; set => SetField(ref _hwRamInfo, value); }
    public string HwRecommendation { get => _hwRecommendation; set => SetField(ref _hwRecommendation, value); }

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
        _assistantName = cfg.AssistantName;
        _personality = cfg.Personality;
        _responseLength = cfg.ResponseLength;
        _showHideHotkey = cfg.ShowHideHotkey;
        _toggleModeHotkey = cfg.ToggleModeHotkey;
        // Always read from actual registry state so the checkbox reflects reality
        _launchWithWindows = StartupManager.IsEnabled;
        _saveChatHistory = cfg.SaveChatHistory;
        _closeToTray = cfg.CloseToTray;
        _confirmationAlertsEnabled = cfg.ConfirmationAlertsEnabled;
        _modelKeepAliveMinutes = cfg.ModelKeepAliveMinutes;
        _performanceMode = cfg.PerformanceMode;
    }

    public AppConfig BuildConfig() => new()
    {
        AccentColor = AccentColor,
        OverlayOpacity = OverlayOpacity,
        FontSize = FontSize,
        OverlayPosition = OverlayPosition,
        OllamaModel = OllamaModel,
        AssistantName = AssistantName,
        Personality = Personality,
        ResponseLength = ResponseLength,
        ShowHideHotkey = ShowHideHotkey,
        ToggleModeHotkey = ToggleModeHotkey,
        LaunchWithWindows = LaunchWithWindows,
        SaveChatHistory = SaveChatHistory,
        CloseToTray = CloseToTray,
        ConfirmationAlertsEnabled = ConfirmationAlertsEnabled,
        ModelKeepAliveMinutes = ModelKeepAliveMinutes,
        PerformanceMode = PerformanceMode,
        SetupCompleted = _configManager.Config.SetupCompleted
    };

    public void Save()
    {
        var config = BuildConfig();
        _configManager.Save(config);
        ThemeEngine.Apply(config);
        StartupManager.SetEnabled(LaunchWithWindows);
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
}
