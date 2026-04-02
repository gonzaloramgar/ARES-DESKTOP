using System.Diagnostics;

namespace AresAssistant.Core;

public static class RuntimeModelAdvisor
{
    public static List<string> ReorderCandidates(List<string> candidates)
    {
        if (candidates.Count <= 1) return candidates;

        var (cpu, ramLoad) = ReadSystemLoad();
        var highPressure = cpu >= 85 || ramLoad >= 85;
        if (!highPressure) return candidates;

        return candidates
            .OrderBy(m => IsLightweightModel(m) ? 0 : 1)
            .ThenBy(m => m, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsLightweightModel(string model)
    {
        var m = (model ?? string.Empty).ToLowerInvariant();
        return m.Contains(":3b") || m.Contains("moondream") || m.Contains("phi") || m.Contains("tiny");
    }

    private static (float cpu, float ramLoad) ReadSystemLoad()
    {
        try
        {
            using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            cpuCounter.NextValue();
            Thread.Sleep(80);
            var cpu = cpuCounter.NextValue();

            using var freeMemCounter = new PerformanceCounter("Memory", "Available MBytes");
            var freeMb = freeMemCounter.NextValue();

            var totalBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            var totalMb = totalBytes > 0 ? totalBytes / 1024f / 1024f : 0f;
            var usedPct = totalMb > 0 ? Math.Clamp((1f - (freeMb / totalMb)) * 100f, 0f, 100f) : 0f;

            return (cpu, usedPct);
        }
        catch
        {
            return (0f, 0f);
        }
    }
}
