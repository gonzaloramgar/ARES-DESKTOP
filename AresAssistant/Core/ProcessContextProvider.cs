using System.Diagnostics;

namespace AresAssistant.Core;

public sealed class ProcessContextProvider
{
    private static readonly HashSet<string> Ignored = new(StringComparer.OrdinalIgnoreCase)
    {
        "Idle", "System", "Registry", "svchost", "dwm", "csrss", "wininit", "services",
        "taskhostw", "fontdrvhost", "RuntimeBroker", "SearchHost", "StartMenuExperienceHost",
        "ShellExperienceHost", "LockApp", "ApplicationFrameHost", "explorer"
    };

    public string GetCompactContext(int maxItems = 6)
    {
        try
        {
            var names = Process.GetProcesses()
                .Select(p => p.ProcessName)
                .Where(n => !string.IsNullOrWhiteSpace(n) && !Ignored.Contains(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .Take(Math.Max(1, maxItems))
                .ToList();

            return names.Count == 0 ? "sin apps destacables" : string.Join(", ", names);
        }
        catch
        {
            return "contexto de procesos no disponible";
        }
    }
}
