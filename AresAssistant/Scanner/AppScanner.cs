using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Scanner;

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

    private static JObject CreateAppEntry(string displayName, string path) => new()
    {
        ["type"] = "open_app",
        ["display_name"] = displayName,
        ["path"] = path
    };

    public static string MakeKey(string prefix, string name)
    {
        var clean = new System.Text.RegularExpressions.Regex(@"[^a-z0-9]+")
            .Replace(name.ToLowerInvariant(), "_")
            .Trim('_');
        return $"{prefix}_{clean}";
    }
}
