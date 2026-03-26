using System.Text.RegularExpressions;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Core;

public class AppScanner
{
    public event Action<string>? StatusChanged;

    public Dictionary<string, JObject> Scan()
    {
        var tools = new Dictionary<string, JObject>();

        StatusChanged?.Invoke("Escaneando aplicaciones instaladas...");

        ScanRegistry(tools, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        ScanRegistry(tools, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
        ScanStartMenu(tools);
        ScanSteamApps(tools);
        ScanEpicGamesApps(tools);
        ScanDesktopShortcuts(tools);
        ScanCustomApps(tools);

        return tools;
    }

    private static void ScanRegistry(Dictionary<string, JObject> tools, string keyPath)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            if (key == null) return;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                try
                {
                    using var subKey = key.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    var name = subKey.GetValue("DisplayName") as string;
                    var exePath = subKey.GetValue("DisplayIcon") as string
                               ?? subKey.GetValue("InstallLocation") as string;

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(exePath))
                        continue;

                    // Clean up exe path (may have ",0" suffix from display icons)
                    exePath = exePath.Split(',')[0].Trim('"', ' ');

                    if (!File.Exists(exePath) || !exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var toolKey = MakeKey("open", name);
                    if (!tools.ContainsKey(toolKey))
                        tools[toolKey] = CreateAppEntry(name, exePath);
                }
                catch { /* skip bad keys */ }
            }
        }
        catch { /* skip entire key if inaccessible */ }
    }

    private static void ScanStartMenu(Dictionary<string, JObject> tools)
    {
        var startMenuPaths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
        };

        foreach (var startMenu in startMenuPaths)
        {
            if (!Directory.Exists(startMenu)) continue;

            string[] lnkFiles;
            try
            {
                var enumOpts = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true
                };
                lnkFiles = Directory.GetFiles(startMenu, "*.lnk", enumOpts);
            }
            catch { continue; } // skip this start menu path if inaccessible

            foreach (var lnk in lnkFiles)
            {
                try
                {
                    var targetPath = ResolveLnk(lnk);
                    if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath)) continue;

                    var name = Path.GetFileNameWithoutExtension(lnk);
                    var toolKey = MakeKey("open", name);

                    if (!tools.ContainsKey(toolKey))
                        tools[toolKey] = CreateAppEntry(name, targetPath);
                }
                catch { /* skip bad shortcuts */ }
            }
        }
    }

    private static string? ResolveLnk(string lnkPath)
    {
        try
        {
            // Use WshShortcut via COM if available, else fallback
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            dynamic shortcut = shell.CreateShortcut(lnkPath);
            return shortcut.TargetPath as string;
        }
        catch
        {
            return null;
        }
    }

    // ───── Steam games ─────

    private static readonly HashSet<string> _ignoredExeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "uninstall", "unitycrashandler64", "unitycrashandler32",
        "crashhandler", "launcher", "updater", "setup", "redist"
    };

    private static void ScanSteamApps(Dictionary<string, JObject> tools)
    {
        foreach (var commonDir in GetSteamLibraryPaths())
        {
            if (!Directory.Exists(commonDir)) continue;

            try
            {
                foreach (var gameDir in Directory.GetDirectories(commonDir))
                {
                    try
                    {
                        var gameName = Path.GetFileName(gameDir);
                        var exe = PickBestExe(gameDir, gameName);
                        if (exe == null) continue;

                        var toolKey = MakeKey("open", gameName);
                        if (!tools.ContainsKey(toolKey))
                            tools[toolKey] = CreateAppEntry(gameName, exe);
                    }
                    catch { /* skip game folder */ }
                }
            }
            catch { /* skip library folder */ }
        }
    }

    private static List<string> GetSteamLibraryPaths()
    {
        var steamRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        var paths = new List<string>();

        var defaultCommon = Path.Combine(steamRoot, "steamapps", "common");
        if (Directory.Exists(defaultCommon))
            paths.Add(defaultCommon);

        // Parse libraryfolders.vdf for extra library locations
        var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
        {
            try
            {
                foreach (var line in File.ReadAllLines(vdf))
                {
                    var m = Regex.Match(line, @"""path""\s+""(.+?)""");
                    if (!m.Success) continue;

                    var libCommon = Path.Combine(
                        m.Groups[1].Value.Replace(@"\\", @"\"), "steamapps", "common");
                    if (Directory.Exists(libCommon) && !paths.Contains(libCommon, StringComparer.OrdinalIgnoreCase))
                        paths.Add(libCommon);
                }
            }
            catch { /* ignore vdf parse errors */ }
        }

        return paths;
    }

    private static string? PickBestExe(string gameDir, string gameName)
    {
        string[] exes;
        try { exes = Directory.GetFiles(gameDir, "*.exe", SearchOption.TopDirectoryOnly); }
        catch { return null; }

        if (exes.Length == 0) return null;

        var folderNorm = gameName.Replace(" ", "").ToLowerInvariant();

        // 1. Prefer exe whose name matches the folder name
        foreach (var exe in exes)
        {
            var exeName = Path.GetFileNameWithoutExtension(exe).ToLowerInvariant();
            if (_ignoredExeNames.Contains(exeName)) continue;
            var exeNorm = exeName.Replace(" ", "");
            if (exeNorm == folderNorm || exeNorm.Contains(folderNorm) || folderNorm.Contains(exeNorm))
                return exe;
        }

        // 2. Fallback: first exe that isn't an ignored utility
        return exes.FirstOrDefault(e =>
            !_ignoredExeNames.Contains(Path.GetFileNameWithoutExtension(e).ToLowerInvariant()));
    }

    // ───── Desktop shortcuts (.lnk + .url) ─────

    private static void ScanDesktopShortcuts(Dictionary<string, JObject> tools)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (!Directory.Exists(desktop)) return;

        var opts = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };

        // .lnk shortcuts
        try
        {
            foreach (var lnk in Directory.GetFiles(desktop, "*.lnk", opts))
            {
                try
                {
                    var target = ResolveLnk(lnk);
                    if (string.IsNullOrEmpty(target) || !File.Exists(target)) continue;
                    if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

                    var name = Path.GetFileNameWithoutExtension(lnk);
                    var toolKey = MakeKey("open", name);
                    if (!tools.ContainsKey(toolKey))
                        tools[toolKey] = CreateAppEntry(name, target);
                }
                catch { /* skip */ }
            }
        }
        catch { /* skip */ }

        // .url shortcuts (Steam games, etc.)
        try
        {
            foreach (var url in Directory.GetFiles(desktop, "*.url", opts))
            {
                try
                {
                    var name = Path.GetFileNameWithoutExtension(url);
                    var urlTarget = ParseUrlShortcut(url);
                    if (string.IsNullOrEmpty(urlTarget)) continue;

                    var toolKey = MakeKey("open", name);
                    if (!tools.ContainsKey(toolKey))
                        tools[toolKey] = CreateAppEntry(name, urlTarget);
                }
                catch { /* skip */ }
            }
        }
        catch { /* skip */ }
    }

    private static string? ParseUrlShortcut(string urlPath)
    {
        foreach (var line in File.ReadAllLines(urlPath))
        {
            if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                return line.Substring(4).Trim();
        }
        return null;
    }

    private static JObject CreateAppEntry(string displayName, string path) => new()
    {
        ["type"] = "open_app",
        ["display_name"] = displayName,
        ["path"] = path
    };

    // ───── Epic Games ─────

    private static void ScanEpicGamesApps(Dictionary<string, JObject> tools)
    {
        var manifestDir = @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests";
        if (!Directory.Exists(manifestDir)) return;

        try
        {
            foreach (var file in Directory.GetFiles(manifestDir, "*.item"))
            {
                try
                {
                    var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(file));

                    var displayName = json["DisplayName"]?.ToString();
                    var installLocation = json["InstallLocation"]?.ToString()?.Replace('/', '\\');
                    var launchExe = json["LaunchExecutable"]?.ToString()?.Replace('/', '\\');

                    if (string.IsNullOrWhiteSpace(displayName) ||
                        string.IsNullOrWhiteSpace(installLocation) ||
                        string.IsNullOrWhiteSpace(launchExe))
                        continue;

                    var fullExe = Path.Combine(installLocation, launchExe);
                    if (!File.Exists(fullExe)) continue;

                    var toolKey = MakeKey("open", displayName);
                    if (!tools.ContainsKey(toolKey))
                        tools[toolKey] = CreateAppEntry(displayName, fullExe);
                }
                catch { /* skip bad manifest */ }
            }
        }
        catch { /* skip if inaccessible */ }
    }

    // ───── Custom / user-remembered apps ─────

    private const string CustomAppsFile = "data/custom-apps.json";

    private static void ScanCustomApps(Dictionary<string, JObject> tools)
    {
        if (!File.Exists(CustomAppsFile)) return;
        try
        {
            var dict = Newtonsoft.Json.JsonConvert.DeserializeObject<
                Dictionary<string, CustomAppEntry>>(File.ReadAllText(CustomAppsFile));
            if (dict == null) return;

            foreach (var (key, entry) in dict)
            {
                var toolKey = MakeKey("open", key);
                if (!tools.ContainsKey(toolKey))
                    tools[toolKey] = CreateAppEntry(entry.DisplayName, entry.Path);
            }
        }
        catch { /* ignore corrupt file */ }
    }

    /// <summary>
    /// Saves a user-provided app so it is remembered across sessions.
    /// Returns true if saved successfully.
    /// </summary>
    public static bool SaveCustomApp(string name, string path)
    {
        try
        {
            var dict = new Dictionary<string, CustomAppEntry>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(CustomAppsFile))
            {
                var existing = Newtonsoft.Json.JsonConvert.DeserializeObject<
                    Dictionary<string, CustomAppEntry>>(File.ReadAllText(CustomAppsFile));
                if (existing != null)
                    foreach (var kv in existing) dict[kv.Key] = kv.Value;
            }

            dict[name.ToLowerInvariant()] = new CustomAppEntry { DisplayName = name, Path = path };

            var dir = Path.GetDirectoryName(CustomAppsFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(CustomAppsFile,
                Newtonsoft.Json.JsonConvert.SerializeObject(dict, Newtonsoft.Json.Formatting.Indented));
            return true;
        }
        catch { return false; }
    }

    private class CustomAppEntry
    {
        public string DisplayName { get; set; } = "";
        public string Path { get; set; } = "";
    }

    public static string MakeKey(string prefix, string name)
    {
        var clean = new System.Text.RegularExpressions.Regex(@"[^a-z0-9]+")
            .Replace(name.ToLowerInvariant(), "_")
            .Trim('_');
        return $"{prefix}_{clean}";
    }
}
