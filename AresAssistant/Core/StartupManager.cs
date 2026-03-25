using Microsoft.Win32;
using System.Diagnostics;

namespace AresAssistant.Core;

/// <summary>
/// Manages the Windows startup registry entry for ARES.
/// Uses HKCU\Software\Microsoft\Windows\CurrentVersion\Run so no admin rights are needed.
/// </summary>
public static class StartupManager
{
    private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "ARES";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, writable: false);
            return key?.GetValue(AppName) is not null;
        }
    }

    public static void SetEnabled(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, writable: true);
        if (key is null) return;

        if (enable)
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (!string.IsNullOrEmpty(exePath))
                key.SetValue(AppName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }
}
