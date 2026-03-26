using System.Management;
using System.Runtime.InteropServices;

namespace AresAssistant.Core;

public record HardwareInfo(double TotalRamGb, int CpuCores, string CpuName, string RecommendedMode);

public static class HardwareDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public static HardwareInfo Detect()
    {
        // RAM
        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        GlobalMemoryStatusEx(ref mem);
        double totalRamGb = Math.Round(mem.ullTotalPhys / (1024.0 * 1024 * 1024), 1);

        // CPU
        int cores = Environment.ProcessorCount;
        string cpuName = "Desconocido";
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                cpuName = obj["Name"]?.ToString()?.Trim() ?? cpuName;
                break;
            }
        }
        catch { /* WMI not available */ }

        // Recommendation: "avanzado" only if ≥16 GB RAM and ≥8 cores
        string recommended = (totalRamGb >= 16 && cores >= 8) ? "avanzado" : "ligero";

        return new HardwareInfo(totalRamGb, cores, cpuName, recommended);
    }
}
