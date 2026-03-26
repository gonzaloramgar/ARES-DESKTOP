using Newtonsoft.Json.Linq;

namespace AresAssistant.Core;

public enum PermissionLevel
{
    Auto,
    Confirm,
    Blocked
}

public class PermissionManager
{
    private static readonly HashSet<string> ConfirmTools = new()
    {
        "close_app", "write_file", "run_command", "clipboard_write", "type_text", "delete_folder"
    };

    private static readonly string[] BlockedPathPrefixes =
    {
        @"C:\Windows\",
        @"C:\Program Files\",
        @"C:\Program Files (x86)\",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ARES", "data") + @"\",
        Environment.GetEnvironmentVariable("SYSTEMROOT") + @"\System32\"
    };

    public PermissionLevel GetLevel(string toolName, Dictionary<string, JToken> args)
    {
        if (IsPathBlocked(toolName, args))
            return PermissionLevel.Blocked;

        if (ConfirmTools.Contains(toolName))
            return PermissionLevel.Confirm;

        return PermissionLevel.Auto;
    }

    private static bool IsPathBlocked(string toolName, Dictionary<string, JToken> args)
    {
        if (toolName != "write_file" && toolName != "run_command")
            return false;

        if (args.TryGetValue("path", out var pathToken))
        {
            // Canonicalize to prevent traversal bypass (e.g. C:\Windows\..\..\target)
            string path;
            try { path = Path.GetFullPath(pathToken.ToString()); }
            catch { return true; } // malformed path → block

            foreach (var prefix in BlockedPathPrefixes)
            {
                if (!string.IsNullOrEmpty(prefix) &&
                    path.StartsWith(Path.GetFullPath(prefix), StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Block writing inside ARES data directory
            var dataDir = Path.GetFullPath("data");
            if (path.StartsWith(dataDir, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
