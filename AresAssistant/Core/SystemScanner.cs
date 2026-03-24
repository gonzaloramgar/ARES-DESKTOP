using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Core;

public class SystemScanner
{
    private readonly AppScanner _appScanner = new();
    private readonly FolderScanner _folderScanner = new();
    private readonly BrowserScanner _browserScanner = new();

    public event Action<string>? StatusChanged;

    public async Task<Dictionary<string, JObject>> ScanAsync()
    {
        var all = new Dictionary<string, JObject>();

        void Forward(string msg) => StatusChanged?.Invoke(msg);

        _appScanner.StatusChanged += Forward;
        _folderScanner.StatusChanged += Forward;
        _browserScanner.StatusChanged += Forward;

        StatusChanged?.Invoke("Inicializando ARES...");
        await Task.Delay(300);

        try
        {
            var apps = await Task.Run(() => _appScanner.Scan());
            foreach (var (k, v) in apps) all[k] = v;
        }
        catch { StatusChanged?.Invoke("Advertencia: escaneo de apps incompleto."); }

        try
        {
            var folders = await Task.Run(() => _folderScanner.Scan());
            foreach (var (k, v) in folders) all[k] = v;
        }
        catch { StatusChanged?.Invoke("Advertencia: escaneo de carpetas incompleto."); }

        try
        {
            var browsers = await Task.Run(() => _browserScanner.Scan());
            foreach (var (k, v) in browsers) all[k] = v;
        }
        catch { StatusChanged?.Invoke("Advertencia: escaneo de navegadores incompleto."); }

        StatusChanged?.Invoke("Generando base de herramientas...");
        await Task.Delay(200);

        return all;
    }

    public static void SaveToJson(Dictionary<string, JObject> tools, string path)
    {
        var json = JsonConvert.SerializeObject(tools, Formatting.Indented);
        File.WriteAllText(path, json);
    }
}
