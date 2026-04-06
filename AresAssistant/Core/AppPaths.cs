namespace AresAssistant.Core;

public static class AppPaths
{
    public static string BaseDirectory => AppContext.BaseDirectory;
    public static string DataDirectory => Path.Combine(BaseDirectory, "data");
    public static string LogsDirectory => Path.Combine(DataDirectory, "logs");

    public static string ConfigFile => Path.Combine(DataDirectory, "config.json");
    public static string ToolsFile => Path.Combine(DataDirectory, "tools.json");
    public static string ChatHistoryFile => Path.Combine(DataDirectory, "chat-history.json");
    public static string SecurityPolicyFile => Path.Combine(DataDirectory, "security-policy.json");
    public static string MemoryFile => Path.Combine(DataDirectory, "memory.json");
    public static string ScheduledTasksFile => Path.Combine(DataDirectory, "scheduled-tasks.json");
    public static string ProductivityFile => Path.Combine(DataDirectory, "productivity.json");
    public static string ReliabilityTelemetryFile => Path.Combine(DataDirectory, "reliability-telemetry.json");
    public static string WeatherCacheFile => Path.Combine(DataDirectory, "weather-cache.json");
    public static string CustomAppsFile => Path.Combine(DataDirectory, "custom-apps.json");
    public static string PluginsDirectory => Path.Combine(DataDirectory, "plugins");
    public static string TtsDirectory => Path.Combine(DataDirectory, "tts");
    public static string OllamaDebugLogFile => Path.Combine(LogsDirectory, "ollama_debug.log");
    public static string RuntimeActionsLogFile => Path.Combine(LogsDirectory, $"runtime_actions_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");

    public static string DataFile(string fileName) => Path.Combine(DataDirectory, fileName);

    public static void EnsureDataDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(TtsDirectory);
    }

    public static string ProjectScopeName
    {
        get
        {
            var name = typeof(AppPaths).Assembly.GetName().Name;
            return string.IsNullOrWhiteSpace(name) ? "general" : name;
        }
    }
}
