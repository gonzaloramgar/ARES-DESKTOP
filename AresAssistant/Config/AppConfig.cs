namespace AresAssistant.Config;

public record AppConfig
{
    public string AccentColor { get; init; } = "#ff2222";
    public double OverlayOpacity { get; init; } = 0.85;
    public string FontSize { get; init; } = "medium";
    public string OverlayPosition { get; init; } = "bottom-right";
    public string OllamaModel { get; init; } = "qwen2.5:7b";
    public string AssistantName { get; init; } = "ARES";
    public string Personality { get; init; } = "formal";
    public string ResponseLength { get; init; } = "normal";
    public string ShowHideHotkey { get; init; } = "Ctrl+Space";
    public string ToggleModeHotkey { get; init; } = "Ctrl+Shift+Space";
    public bool LaunchWithWindows { get; init; } = false;
    public bool SaveChatHistory { get; init; } = true;
    public bool CloseToTray { get; init; } = true;
    public bool ConfirmationAlertsEnabled { get; init; } = true;
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
    public int ModelKeepAliveMinutes { get; init; } = 5;
    public bool SetupCompleted { get; init; } = false;

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
            //  7b: ctx 4K, 4 hilos fijos, batch estándar.
            _          => (4096, 4, 20, 512, 512),
        };
    }
}
