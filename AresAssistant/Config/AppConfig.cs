namespace AresAssistant.Config;

public record AppConfig
{
    public string AccentColor { get; init; } = "#ff2222";
    public double OverlayOpacity { get; init; } = 0.85;
    public string FontSize { get; init; } = "medium";
    public string OverlayPosition { get; init; } = "bottom-right";
    public string OllamaModel { get; init; } = "qwen2.5:32b";
    public string AssistantName { get; init; } = "ARES";
    public string Personality { get; init; } = "formal";
    public string ResponseLength { get; init; } = "normal";
    public string ShowHideHotkey { get; init; } = "Ctrl+Space";
    public string ToggleModeHotkey { get; init; } = "Ctrl+Shift+Space";
    public bool LaunchWithWindows { get; init; } = false;
    public bool SaveChatHistory { get; init; } = true;
    public bool CloseToTray { get; init; } = true;
    public bool ConfirmationAlertsEnabled { get; init; } = true;
    /// <summary>
    /// Minutes of inactivity before telling Ollama to unload the model from RAM.
    /// 0 = never auto-unload.
    /// </summary>
    public int ModelKeepAliveMinutes { get; init; } = 5;
    public bool SetupCompleted { get; init; } = false;
}
