namespace AresAssistant.Config;

/// <summary>
/// Record inmutable con todos los ajustes de la aplicación.
/// Cada propiedad tiene un valor por defecto seguro para la primera ejecución.
/// </summary>
public record AppConfig
{
    public string AccentColor { get; init; } = "#ff2222";
    public double OverlayOpacity { get; init; } = 0.85;
    public string FontSize { get; init; } = "medium";
    public string OverlayPosition { get; init; } = "bottom-right";
    public string OllamaModel { get; init; } = "qwen2.5:3b";
    /// <summary>Enable local multi-model routing with automatic fallback.</summary>
    public bool MultiModelEnabled { get; init; } = true;
    /// <summary>Always restrict model routing to free local Ollama models.</summary>
    public bool MultiModelFreeOnly { get; init; } = true;
    /// <summary>Preferred local model for coding/debug tasks.</summary>
    public string MultiModelCodingModel { get; init; } = "qwen2.5-coder:3b";
    /// <summary>Preferred local model for reasoning/general tasks.</summary>
    public string MultiModelReasoningModel { get; init; } = "qwen2.5:3b";
    /// <summary>Preferred local model for vision/screenshot tasks.</summary>
    public string MultiModelVisionModel { get; init; } = "moondream:latest";
    /// <summary>Comma-separated local fallback models used if primary fails.</summary>
    public string MultiModelFallbacks { get; init; } = "qwen2.5:3b,qwen2.5-coder:3b,moondream:latest";
    public string AssistantName { get; init; } = "ARES";
    public string Personality { get; init; } = "formal";
    public string ContextProfile { get; init; } = "trabajo";
    public string ResponseLength { get; init; } = "normal";
    public string ShowHideHotkey { get; init; } = "Ctrl+Space";
    public string ToggleModeHotkey { get; init; } = "Ctrl+Shift+Space";
    public bool LaunchWithWindows { get; init; } = false;
    public bool SaveChatHistory { get; init; } = true;
    public bool CloseToTray { get; init; } = true;
    public bool ConfirmationAlertsEnabled { get; init; } = true;
    /// <summary>
    /// When enabled, confirmation-level tool actions are auto-approved for agent chaining.
    /// </summary>
    public bool AutonomousMode { get; init; } = false;
    /// <summary>
    /// Inject active app/process context into prompts to improve task relevance.
    /// </summary>
    public bool ProcessContextEnabled { get; init; } = true;
    /// <summary>Enable daily scheduled automations managed by ARES.</summary>
    public bool ScheduledAutomationsEnabled { get; init; } = true;
    /// <summary>Enable smart clipboard monitoring and contextual suggestions.</summary>
    public bool ClipboardSmartEnabled { get; init; } = true;
    /// <summary>Show weather widget in Full HUD dashboard.</summary>
    public bool WidgetWeatherEnabled { get; init; } = true;
    /// <summary>Show world clock widget in Full HUD dashboard.</summary>
    public bool WidgetWorldClockEnabled { get; init; } = true;
    /// <summary>Show live CPU/RAM widget in Full HUD dashboard.</summary>
    public bool WidgetSystemLiveEnabled { get; init; } = true;
    /// <summary>Show upcoming scheduled tasks widget in Full HUD dashboard.</summary>
    public bool WidgetTasksEnabled { get; init; } = true;
    /// <summary>Show productivity tracker panel in Full HUD dashboard.</summary>
    public bool WidgetProductivityEnabled { get; init; } = true;
    /// <summary>Enable text-to-speech for assistant responses.</summary>
    public bool VoiceEnabled { get; init; } = true;
    /// <summary>TTS playback volume 0.0–1.0. Default 0.5 (50%).</summary>
    public float TtsVolume { get; init; } = 0.5f;
    /// <summary>"masculino" or "femenino". Selects voice gender across all TTS engines.</summary>
    public string TtsVoiceGender { get; init; } = "masculino";
    /// <summary>
    /// Minutes of inactivity before telling Ollama to unload the model from RAM.
    /// 0 = never auto-unload.
    /// </summary>
    public int ModelKeepAliveMinutes { get; init; } = 30;
    public bool SetupCompleted { get; init; } = false;
    /// <summary>Last onboarding/tutorial version completed by the user.</summary>
    public string OnboardingVersionSeen { get; init; } = "";

    /// <summary>"ligero" or "avanzado". Controls model params, context window, history trim.</summary>
    public string PerformanceMode { get; init; } = "ligero";

    /// <summary>Return Ollama options tuned for the current mode.</summary>
    public (int NumCtx, int NumThread, int HistoryLimit, int NumPredict, int NumBatch) GetPerformanceParams()
    {
        return PerformanceMode switch
        {
            //  14b: mismo ctx que 7b (4K sobra), batch grande para evaluar prompt rápido,
            //  num_predict=512 evita respuestas interminables, auto-threads.
            "avanzado" => (4096, 0, 20, 512, 1024),
            //  7b: ctx 4K, auto-threads, batch estándar.
            _          => (4096, 0, 20, 512, 512),
        };
    }
}
