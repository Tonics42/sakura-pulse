using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProcessAnalyzerPro.Models;

public class ProcessInfo : INotifyPropertyChanged
{
    private double _cpuUsage;
    private long   _currentRamMb;
    private long   _peakRamMb;
    private int    _networkConnections;
    private TimeSpan _activeDuration;
    private double _diskReadKbps;
    private double _diskWriteKbps;
    private float  _gpuPercent;

    public int    Pid            { get; init; }
    public string Name           { get; init; } = string.Empty;
    public bool   IsAccessDenied { get; init; }

    public double CpuUsage
    {
        get => _cpuUsage;
        set
        {
            _cpuUsage = Math.Round(Math.Max(0, Math.Min(100, value)), 2);
            OnPropertyChanged();
            OnPropertyChanged(nameof(CpuUsageDisplay));
        }
    }

    public long CurrentRamMb
    {
        get => _currentRamMb;
        set { _currentRamMb = value; OnPropertyChanged(); OnPropertyChanged(nameof(CurrentRamDisplay)); }
    }

    public long PeakRamMb
    {
        get => _peakRamMb;
        set { _peakRamMb = value; OnPropertyChanged(); OnPropertyChanged(nameof(PeakRamDisplay)); }
    }

    public int NetworkConnections
    {
        get => _networkConnections;
        set { _networkConnections = value; OnPropertyChanged(); OnPropertyChanged(nameof(NetworkDisplay)); }
    }

    public TimeSpan ActiveDuration
    {
        get => _activeDuration;
        set { _activeDuration = value; OnPropertyChanged(); OnPropertyChanged(nameof(ActiveDurationDisplay)); }
    }

    public double DiskReadKbps
    {
        get => _diskReadKbps;
        set { _diskReadKbps = value; OnPropertyChanged(); OnPropertyChanged(nameof(DiskReadDisplay)); }
    }

    public double DiskWriteKbps
    {
        get => _diskWriteKbps;
        set { _diskWriteKbps = value; OnPropertyChanged(); OnPropertyChanged(nameof(DiskWriteDisplay)); }
    }

    public float GpuPercent
    {
        get => _gpuPercent;
        set { _gpuPercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(GpuDisplay)); }
    }

    // ── Display ─────────────────────────────────────────────────────────────
    public string CpuUsageDisplay    => IsAccessDenied ? "PROT" : $"{CpuUsage:F1}%";
    public string CurrentRamDisplay  => IsAccessDenied ? "---"  : $"{CurrentRamMb} MB";
    public string PeakRamDisplay     => IsAccessDenied ? "---"  : $"{PeakRamMb} MB";
    public string NetworkDisplay     => NetworkConnections > 0 ? $"{NetworkConnections} CONN" : "IDLE";
    public string ActiveDurationDisplay => _activeDuration.ToString(@"hh\:mm\:ss");
    public string DisplayName        => IsAccessDenied ? $"{Name} [Protected]" : Name;

    public string DiskReadDisplay  => _diskReadKbps  >= 0.5 ? $"{_diskReadKbps:F0} KB/s" : "0";
    public string DiskWriteDisplay => _diskWriteKbps >= 0.5 ? $"{_diskWriteKbps:F0} KB/s" : "0";
    public string GpuDisplay       => IsAccessDenied ? "---" :
                                      _gpuPercent >= 0.1f ? $"{_gpuPercent:F1}%" : "0%";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
