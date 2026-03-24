using Newtonsoft.Json.Linq;

namespace AresAssistant.Core;

public class FolderScanner
{
    public event Action<string>? StatusChanged;

    public Dictionary<string, JObject> Scan()
    {
        var tools = new Dictionary<string, JObject>();

        StatusChanged?.Invoke("Detectando carpetas importantes...");

        AddIfExists(tools, "open_desktop", "Escritorio",
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop));

        AddIfExists(tools, "open_documents", "Documentos",
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

        AddIfExists(tools, "open_downloads", "Descargas",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));

        AddIfExists(tools, "open_pictures", "Imágenes",
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));

        AddIfExists(tools, "open_videos", "Vídeos",
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));

        AddIfExists(tools, "open_music", "Música",
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));

        // Scan for git repos and Visual Studio solutions in common locations
        ScanProjectFolders(tools);

        return tools;
    }

    private static void AddIfExists(Dictionary<string, JObject> tools, string key, string name, string path)
    {
        if (Directory.Exists(path))
            tools[key] = new JObject
            {
                ["type"] = "open_folder",
                ["display_name"] = name,
                ["path"] = path
            };
    }

    private static void ScanProjectFolders(Dictionary<string, JObject> tools)
    {
        var searchRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        foreach (var root in searchRoots)
        {
            if (!Directory.Exists(root)) continue;

            // Find git repos (depth limited to avoid scanning whole drive)
            FindFoldersWithMarker(root, ".git", "repo", tools, depth: 3);
            FindFoldersWithMarker(root, "*.sln", "vs", tools, depth: 4);
        }
    }

    private static void FindFoldersWithMarker(string root, string marker, string prefix,
        Dictionary<string, JObject> tools, int depth)
    {
        if (depth <= 0 || !Directory.Exists(root)) return;

        try
        {
            bool hasMarker = marker.Contains("*")
                ? Directory.GetFiles(root, marker).Length > 0
                : Directory.Exists(Path.Combine(root, marker));

            if (hasMarker)
            {
                var name = Path.GetFileName(root);
                var key = AppScanner.MakeKey($"open_{prefix}", name);
                if (!tools.ContainsKey(key))
                    tools[key] = new JObject
                    {
                        ["type"] = "open_folder",
                        ["display_name"] = name,
                        ["path"] = root
                    };
                return; // don't recurse into project folders
            }

            foreach (var sub in Directory.GetDirectories(root))
                FindFoldersWithMarker(sub, marker, prefix, tools, depth - 1);
        }
        catch { /* skip inaccessible dirs */ }
    }
}
