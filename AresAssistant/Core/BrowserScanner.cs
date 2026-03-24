using Newtonsoft.Json.Linq;

namespace AresAssistant.Core;

public class BrowserScanner
{
    public event Action<string>? StatusChanged;

    private static readonly (string DisplayName, string[] KnownPaths)[] KnownBrowsers =
    {
        ("Google Chrome", new[]
        {
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Google\Chrome\Application\chrome.exe")
        }),
        ("Microsoft Edge", new[]
        {
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
        }),
        ("Mozilla Firefox", new[]
        {
            @"C:\Program Files\Mozilla Firefox\firefox.exe",
            @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe"
        }),
        ("Brave", new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"BraveSoftware\Brave-Browser\Application\brave.exe"),
            @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe"
        }),
        ("Opera", new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Programs\Opera\opera.exe")
        })
    };

    public Dictionary<string, JObject> Scan()
    {
        var tools = new Dictionary<string, JObject>();

        StatusChanged?.Invoke("Buscando navegadores...");

        foreach (var (displayName, paths) in KnownBrowsers)
        {
            var found = paths.FirstOrDefault(File.Exists);
            if (found == null) continue;

            var openKey = AppScanner.MakeKey("open", displayName);
            tools[openKey] = new JObject
            {
                ["type"] = "open_app",
                ["display_name"] = displayName,
                ["path"] = found
            };

            var searchKey = AppScanner.MakeKey("search", displayName);
            tools[searchKey] = new JObject
            {
                ["type"] = "search_browser",
                ["display_name"] = displayName,
                ["browser_path"] = found
            };
        }

        return tools;
    }
}
