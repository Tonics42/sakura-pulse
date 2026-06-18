using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ProcessAnalyzerPro.Models;

namespace ProcessAnalyzerPro.Services;

public sealed class ProcessMetricSnapshot
{
    public required int    Pid  { get; init; }
    public required string Name { get; init; }
    public double   CpuUsage           { get; init; }
    public long     RamMb              { get; init; }
    public long     PeakRamMb          { get; init; }
    public int      NetworkConnections  { get; init; }
    public TimeSpan ActiveDuration      { get; init; }
    public bool     IsAccessDenied      { get; init; }
    public double   DiskReadKbps        { get; init; }
    public double   DiskWriteKbps       { get; init; }
    public float    GpuPercent          { get; init; }
}

public sealed class SystemSnapshot
{
    public double TotalCpuPercent { get; init; }
    public double UsedRamGb       { get; init; }
    public double TotalRamGb      { get; init; }
    public double RamPercent      { get; init; }
    public float  TotalGpuPercent { get; init; }
}

public sealed class ProcessMonitorService : IDisposable
{
    // ── P/Invoke — RAM ───────────────────────────────────────────────────────
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx s);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint   dwLength;
        public uint   dwMemoryLoad;
        public ulong  ullTotalPhys;
        public ulong  ullAvailPhys;
        public ulong  ullTotalPageFile;
        public ulong  ullAvailPageFile;
        public ulong  ullTotalVirtual;
        public ulong  ullAvailVirtual;
        public ulong  ullAvailExtendedVirtual;

        public static MemoryStatusEx Create()
        {
            var s = new MemoryStatusEx();
            s.dwLength = (uint)Marshal.SizeOf(s);
            return s;
        }
    }

    // ── P/Invoke — Disk I/O ──────────────────────────────────────────────────
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessIoCounters(IntPtr hProcess, out IoCounters counters);

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;   // cumulative bytes read
        public ulong WriteTransferCount;  // cumulative bytes written
        public ulong OtherTransferCount;
    }

    // ── State ────────────────────────────────────────────────────────────────
    private readonly record struct CpuSample(TimeSpan CpuTime, DateTime Timestamp);
    private readonly record struct IoSample(ulong ReadBytes, ulong WriteBytes, DateTime Timestamp);

    private readonly Dictionary<int, CpuSample> _cpuHistory = new();
    private readonly Dictionary<int, IoSample>  _ioHistory  = new();
    private Dictionary<int, float> _lastGpuMap = new();
    private int  _tickCount;
    private volatile bool _suspended;
    private PerformanceCounter? _sysCpuCounter;
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;

    public event Action<IReadOnlyList<ProcessMetricSnapshot>, SystemSnapshot>? SnapshotReady;

    // ── Control ──────────────────────────────────────────────────────────────
    public void Start(TimeSpan interval)
    {
        if (_pollingTask is { IsCompleted: false }) return;

        _cts = new CancellationTokenSource();
        _sysCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", readOnly: true);
        _sysCpuCounter.NextValue(); // warm-up; first read always returns 0

        _pollingTask = RunLoopAsync(interval, _cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _sysCpuCounter?.Dispose();
        _sysCpuCounter = null;
    }

    public void Suspend() => _suspended = true;
    public void Resume()  => _suspended = false;

    private async Task RunLoopAsync(TimeSpan interval, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (_suspended) continue;
                try { await CollectAsync(); }
                catch (OperationCanceledException) { return; }
                catch { /* snapshot failed; retry next tick */ }
            }
        }
        catch (OperationCanceledException) { }
    }

    private Task CollectAsync() => Task.Run(() =>
    {
        var networkMap = NetworkMonitorService.GetConnectionCountsByPid();
        if (++_tickCount % 3 == 0) _lastGpuMap = GpuMonitorService.GetGpuUsageByPid();
        var gpuMap = _lastGpuMap;
        var processes  = Process.GetProcesses();
        var now        = DateTime.UtcNow;
        var results    = new List<ProcessMetricSnapshot>(processes.Length);

        foreach (var proc in processes)
        {
            try
            {
                int    pid  = proc.Id;
                string name = proc.ProcessName;

                TimeSpan cpuTime;
                long ramBytes, peakRamBytes;
                DateTime startTime;

                try
                {
                    cpuTime      = proc.TotalProcessorTime;
                    ramBytes     = proc.WorkingSet64;
                    peakRamBytes = proc.PeakWorkingSet64;
                    startTime    = proc.StartTime;
                }
                catch (Win32Exception)
                {
                    results.Add(new ProcessMetricSnapshot { Pid = pid, Name = name, IsAccessDenied = true });
                    continue;
                }

                // ── CPU delta ────────────────────────────────────────────
                double cpuPct = 0;
                if (_cpuHistory.TryGetValue(pid, out var prevCpu))
                {
                    double elapsedSec = (now - prevCpu.Timestamp).TotalSeconds;
                    if (elapsedSec > 0)
                        cpuPct = Math.Max(0, Math.Min(100,
                            (cpuTime - prevCpu.CpuTime).TotalSeconds / elapsedSec / Environment.ProcessorCount * 100.0));
                }
                _cpuHistory[pid] = new CpuSample(cpuTime, now);

                // ── Disk I/O delta ───────────────────────────────────────
                double diskReadKbps = 0, diskWriteKbps = 0;
                try
                {
                    if (GetProcessIoCounters(proc.Handle, out IoCounters io))
                    {
                        if (_ioHistory.TryGetValue(pid, out var prevIo))
                        {
                            double elapsedSec = (now - prevIo.Timestamp).TotalSeconds;
                            if (elapsedSec > 0)
                            {
                                diskReadKbps  = Math.Max(0, (io.ReadTransferCount  - prevIo.ReadBytes)  / elapsedSec / 1024.0);
                                diskWriteKbps = Math.Max(0, (io.WriteTransferCount - prevIo.WriteBytes) / elapsedSec / 1024.0);
                            }
                        }
                        _ioHistory[pid] = new IoSample(io.ReadTransferCount, io.WriteTransferCount, now);
                    }
                }
                catch { }

                results.Add(new ProcessMetricSnapshot
                {
                    Pid                = pid,
                    Name               = name,
                    CpuUsage           = cpuPct,
                    RamMb              = ramBytes     / (1024L * 1024),
                    PeakRamMb          = peakRamBytes / (1024L * 1024),
                    NetworkConnections = networkMap.GetValueOrDefault(pid),
                    ActiveDuration     = now - startTime.ToUniversalTime(),
                    DiskReadKbps       = diskReadKbps,
                    DiskWriteKbps      = diskWriteKbps,
                    GpuPercent         = gpuMap.GetValueOrDefault(pid)
                });
            }
            catch { /* process exited mid-read */ }
            finally { proc.Dispose(); }
        }

        // Prune stale PID histories
        var livePids = results.Select(r => r.Pid).ToHashSet();
        foreach (var stale in _cpuHistory.Keys.Where(k => !livePids.Contains(k)).ToList())
            _cpuHistory.Remove(stale);
        foreach (var stale in _ioHistory.Keys.Where(k => !livePids.Contains(k)).ToList())
            _ioHistory.Remove(stale);

        // System-wide metrics
        float rawCpu  = _sysCpuCounter?.NextValue() ?? 0f;
        float totalGpu = gpuMap.Count > 0
            ? Math.Min(100f, gpuMap.Values.Max())
            : 0f;
        var ram = GetRamInfo();

        SnapshotReady?.Invoke(results, new SystemSnapshot
        {
            TotalCpuPercent = Math.Round(rawCpu, 1),
            UsedRamGb       = ram.used,
            TotalRamGb      = ram.total,
            RamPercent      = ram.percent,
            TotalGpuPercent = totalGpu
        });
    });

    private static (double used, double total, double percent) GetRamInfo()
    {
        var s = MemoryStatusEx.Create();
        if (GlobalMemoryStatusEx(ref s))
        {
            double total = s.ullTotalPhys / (1024.0 * 1024 * 1024);
            double avail = s.ullAvailPhys / (1024.0 * 1024 * 1024);
            return (total - avail, total, s.dwMemoryLoad);
        }
        return (0, 0, 0);
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        GpuMonitorService.Cleanup();
    }
}
