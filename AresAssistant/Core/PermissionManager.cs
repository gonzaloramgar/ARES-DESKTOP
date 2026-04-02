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
    private readonly SecurityPolicyStore? _policyStore;

    public bool AutoApproveConfirmations { get; set; }

    public PermissionManager(SecurityPolicyStore? policyStore = null)
    {
        _policyStore = policyStore;
    }

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
        var policy = _policyStore?.Policy;

        if (policy != null && policy.BlockedTools.Any(t => t.Equals(toolName, StringComparison.OrdinalIgnoreCase)))
            return PermissionLevel.Blocked;

        if (IsPathBlocked(toolName, args, policy))
            return PermissionLevel.Blocked;

        if (IsCommandBlocked(toolName, args, policy))
            return PermissionLevel.Blocked;

        if (toolName.StartsWith("plugin_", StringComparison.OrdinalIgnoreCase))
        {
            var pluginAllowed = policy?.AllowedPluginTools.Any(t => t.Equals(toolName, StringComparison.OrdinalIgnoreCase)) == true;
            if (!pluginAllowed)
                return PermissionLevel.Confirm;
        }

        if (policy != null && policy.ConfirmTools.Any(t => t.Equals(toolName, StringComparison.OrdinalIgnoreCase)))
            return AutoApproveConfirmations ? PermissionLevel.Auto : PermissionLevel.Confirm;

        if (AutoApproveConfirmations && ConfirmTools.Contains(toolName))
            return PermissionLevel.Auto;

        if (ConfirmTools.Contains(toolName))
            return PermissionLevel.Confirm;

        return PermissionLevel.Auto;
    }

    private static bool IsPathBlocked(string toolName, Dictionary<string, JToken> args, SecurityPolicy? policy)
    {
        if (toolName != "write_file" && toolName != "run_command")
            return false;

        var allPrefixes = BlockedPathPrefixes
            .Concat(policy?.BlockedPathPrefixes ?? Enumerable.Empty<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (args.TryGetValue("path", out var pathToken))
        {
            // Canonicalize to prevent traversal bypass (e.g. C:\Windows\..\..\target)
            string path;
            try { path = Path.GetFullPath(pathToken.ToString()); }
            catch { return true; } // malformed path → block

            foreach (var prefix in allPrefixes)
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

    private static bool IsCommandBlocked(string toolName, Dictionary<string, JToken> args, SecurityPolicy? policy)
    {
        if (toolName != "run_command")
            return false;

        if (!args.TryGetValue("command", out var commandToken))
            return false;

        var command = commandToken.ToString() ?? string.Empty;
        var customPatterns = policy?.BlockedCommandPatterns ?? new List<string>();

        return customPatterns.Any(p =>
            !string.IsNullOrWhiteSpace(p) &&
            command.Contains(p, StringComparison.OrdinalIgnoreCase));
    }
}
