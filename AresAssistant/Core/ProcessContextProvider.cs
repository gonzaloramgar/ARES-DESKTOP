using System.Diagnostics;
using System.Runtime.InteropServices;

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

    public string GetForegroundProcessName()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return "desconocido";

            _ = GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0)
                return "desconocido";

            using var proc = Process.GetProcessById((int)pid);
            var name = proc.ProcessName;
            return string.IsNullOrWhiteSpace(name) ? "desconocido" : name;
        }
        catch
        {
            return "desconocido";
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}
