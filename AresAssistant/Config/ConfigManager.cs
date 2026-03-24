using Newtonsoft.Json;

namespace AresAssistant.Config;

public class ConfigManager
{
    private readonly string _path;
    public AppConfig Config { get; private set; }

    public ConfigManager(string path)
    {
        _path = path;
        Config = Load();
    }

    private AppConfig Load()
    {
        if (!File.Exists(_path))
            return new AppConfig();

        try
        {
            var json = File.ReadAllText(_path);
            return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        Config = config;
        File.WriteAllText(_path, JsonConvert.SerializeObject(config, Formatting.Indented));
    }

    public void Update(Func<AppConfig, AppConfig> updater)
    {
        Save(updater(Config));
    }
}
