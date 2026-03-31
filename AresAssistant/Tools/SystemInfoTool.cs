using AresAssistant.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace AresAssistant.Tools;

/// <summary>
/// Herramienta para obtener información del sistema en tiempo real:
/// uso de CPU, RAM, disco libre, tiempo de actividad y hora actual.
/// </summary>
public class SystemInfoTool : ITool
{
    public string Name => "get_system_info";
    public string Description => "Obtiene información del sistema: CPU, RAM, disco, tiempo de actividad y hora actual.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new(),
        Required = new()
    };

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        try
        {
            using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            cpuCounter.NextValue(); // first call always 0
            await Task.Delay(100);
            var cpu = cpuCounter.NextValue();

            var ramTotal = GetTotalRam();
            var ramAvail = GetAvailableRam();
            var ramUsed = ramTotal - ramAvail;
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

            var diskInfo = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(d => new { d.Name, FreeMb = d.AvailableFreeSpace / 1024 / 1024 / 1024.0 })
                .ToList();

            var result = new
            {
                cpu_percent = Math.Round(cpu, 1),
                ram_used_gb = Math.Round(ramUsed / 1024.0 / 1024.0 / 1024.0, 2),
                ram_total_gb = Math.Round(ramTotal / 1024.0 / 1024.0 / 1024.0, 2),
                disk_free_gb = diskInfo.FirstOrDefault()?.FreeMb ?? 0,
                uptime_hours = Math.Round(uptime.TotalHours, 2),
                current_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            return new ToolResult(true, JsonConvert.SerializeObject(result, Formatting.Indented));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Error al obtener info del sistema: {ex.Message}");
        }
    }

    private static long GetTotalRam()
    {
        try { return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes; }
        catch { return 0; } // PerformanceCounter may not be available
    }

    private static long GetAvailableRam()
    {
        try
        {
            using var counter = new PerformanceCounter("Memory", "Available Bytes");
            return (long)counter.NextValue();
        }
        catch { return 0; } // PerformanceCounter may not be available
    }
}
