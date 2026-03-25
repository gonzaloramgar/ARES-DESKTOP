namespace AresAssistant.Core;

public class ActionLogger
{
    private readonly string _logPath;
    private const long MaxLogBytes = 10 * 1024 * 1024; // 10MB

    public ActionLogger(string logsDirectory)
    {
        Directory.CreateDirectory(logsDirectory);
        var sessionStamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _logPath = Path.Combine(logsDirectory, $"actions_{sessionStamp}.log");
    }

    public void Log(PermissionLevel level, string toolName, object? args = null)
    {
        RotateIfNeeded();

        var levelStr = level switch
        {
            PermissionLevel.Auto => "AUTO",
            PermissionLevel.Confirm => "CONFIRM",
            PermissionLevel.Blocked => "BLOCKED",
            _ => "UNKNOWN"
        };

        var argsStr = args != null
            ? Newtonsoft.Json.JsonConvert.SerializeObject(args)
            : "{}";

        var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {levelStr} | {toolName} | {argsStr}";
        File.AppendAllText(_logPath, entry + Environment.NewLine);
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(_logPath)) return;

        var info = new FileInfo(_logPath);
        if (info.Length <= MaxLogBytes) return;

        var archive = _logPath + $".{DateTime.Now:yyyyMMddHHmmss}.bak";
        File.Move(_logPath, archive);
        PruneOldBackups();
    }

    private void PruneOldBackups(int keep = 3)
    {
        var dir = Path.GetDirectoryName(_logPath)!;
        var prefix = Path.GetFileName(_logPath) + ".";
        var backups = Directory.GetFiles(dir, prefix + "*.bak")
            .OrderByDescending(f => f)
            .Skip(keep);
        foreach (var old in backups)
            try { File.Delete(old); } catch { }
    }
}
