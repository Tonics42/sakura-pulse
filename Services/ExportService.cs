using System.IO;
using System.Text;
using System.Text.Json;
using ProcessAnalyzerPro.Models;

namespace ProcessAnalyzerPro.Services;

public static class ExportService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true
    };

    public static async Task ExportJsonAsync(IEnumerable<ProcessInfo> processes, string filePath)
    {
        var payload = new
        {
            ExportedAt    = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Generator     = "Sakura Pulse v1.0",
            Processes     = processes.Select(p => new
            {
                p.Pid,
                Name             = p.DisplayName,
                CpuUsagePct      = p.CpuUsage,
                PeakRamMb        = p.PeakRamMb,
                NetworkDisplay   = p.NetworkDisplay,
                ActiveDuration   = p.ActiveDurationDisplay,
                Protected        = p.IsAccessDenied
            }).ToList()
        };

        var json = JsonSerializer.Serialize(payload, JsonOpts);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
    }

    public static async Task ExportCsvAsync(IEnumerable<ProcessInfo> processes, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name,PID,CPU%,PeakRamMb,Network,Duration,Protected");

        foreach (var p in processes)
        {
            string safeName = p.DisplayName.Replace(",", ";");
            sb.AppendLine($"{safeName},{p.Pid},{p.CpuUsage:F2},{p.PeakRamMb},{p.NetworkDisplay},{p.ActiveDurationDisplay},{p.IsAccessDenied}");
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }
}
