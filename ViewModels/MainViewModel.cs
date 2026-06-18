using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using ProcessAnalyzerPro.Models;
using ProcessAnalyzerPro.Services;

namespace ProcessAnalyzerPro.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    // ── Fields ──────────────────────────────────────────────────────────────
    private readonly ProcessMonitorService _monitor = new();
    private readonly ObservableCollection<ProcessInfo>   _processes = new();
    private readonly ObservableCollection<StartupEntry>  _startup   = new();
    private readonly ICollectionView _filteredView;

    private bool     _isMonitoring;
    private string   _searchText    = string.Empty;
    private int      _filteredCount;
    private double   _totalCpuPercent;
    private double   _totalRamPercent;
    private string   _totalRamLabel  = "--- / --- GB";
    private float    _totalGpuPercent;
    private string   _sessionTime    = "00:00:00";
    private string   _mascotLevel    = "Low";
    private string   _mascotStatusText = "SYSTEM STABLE";
    private DateTime _sessionStart;
    private ProcessInfo? _selectedProcess;
    private System.Windows.Threading.DispatcherTimer? _sessionTimer;

    // ── Constructor ─────────────────────────────────────────────────────────
    public MainViewModel()
    {
        _filteredView = CollectionViewSource.GetDefaultView(_processes);
        _filteredView.Filter = FilterProcess;
        _filteredView.SortDescriptions.Add(
            new SortDescription(nameof(ProcessInfo.CpuUsage), ListSortDirection.Descending));

        StartStopCommand      = new RelayCommand(ToggleMonitoring);
        ExportJsonCommand     = new RelayCommand(ExportJson,    () => _processes.Count > 0);
        ExportCsvCommand      = new RelayCommand(ExportCsv,     () => _processes.Count > 0);
        RefreshStartupCommand = new RelayCommand(LoadStartupEntries);
        KillProcessCommand    = new RelayCommand(KillSelectedProcess, () => SelectedProcess != null && !SelectedProcess.IsAccessDenied);
        OpenLocationCommand   = new RelayCommand(OpenFileLocation,    () => SelectedProcess != null && !SelectedProcess.IsAccessDenied);
        SetPriorityCommand    = new RelayCommand<string>(SetSelectedPriority);

        _monitor.SnapshotReady += OnSnapshotReady;
        LoadStartupEntries();
    }

    // ── Public Properties ────────────────────────────────────────────────────
    public ICollectionView                    FilteredProcesses => _filteredView;
    public ObservableCollection<StartupEntry> StartupEntries    => _startup;

    public ProcessInfo? SelectedProcess
    {
        get => _selectedProcess;
        set
        {
            _selectedProcess = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsMonitoring
    {
        get => _isMonitoring;
        private set { _isMonitoring = value; OnPropertyChanged(); OnPropertyChanged(nameof(ToggleButtonLabel)); }
    }

    public string ToggleButtonLabel => IsMonitoring ? "STOP  MONITORING" : "START MONITORING";

    public int FilteredCount
    {
        get => _filteredCount;
        private set { _filteredCount = value; OnPropertyChanged(); }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            _filteredView.Refresh();
            FilteredCount = _filteredView.Cast<object>().Count();
        }
    }

    public double TotalCpuPercent
    {
        get => _totalCpuPercent;
        private set { _totalCpuPercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalCpuLabel)); UpdateMascot(); }
    }

    public string TotalCpuLabel => $"{TotalCpuPercent:F0}%";

    public double TotalRamPercent
    {
        get => _totalRamPercent;
        private set { _totalRamPercent = value; OnPropertyChanged(); }
    }

    public string TotalRamLabel
    {
        get => _totalRamLabel;
        private set { _totalRamLabel = value; OnPropertyChanged(); }
    }

    public float TotalGpuPercent
    {
        get => _totalGpuPercent;
        private set { _totalGpuPercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalGpuLabel)); }
    }

    public string TotalGpuLabel => TotalGpuPercent < 0.5f ? "N/A" : $"{TotalGpuPercent:F0}%";

    public string SessionTime
    {
        get => _sessionTime;
        private set { _sessionTime = value; OnPropertyChanged(); }
    }

    public string MascotLevel
    {
        get => _mascotLevel;
        private set { _mascotLevel = value; OnPropertyChanged(); }
    }

    public string MascotStatusText
    {
        get => _mascotStatusText;
        private set { _mascotStatusText = value; OnPropertyChanged(); }
    }

    // ── Commands ─────────────────────────────────────────────────────────────
    public ICommand StartStopCommand      { get; }
    public ICommand ExportJsonCommand     { get; }
    public ICommand ExportCsvCommand      { get; }
    public ICommand RefreshStartupCommand { get; }
    public ICommand KillProcessCommand    { get; }
    public ICommand OpenLocationCommand   { get; }
    public ICommand SetPriorityCommand    { get; }

    // ── Monitoring ────────────────────────────────────────────────────────────
    public void SuspendMonitoring() => _monitor.Suspend();
    public void ResumeMonitoring()  => _monitor.Resume();

    private void ToggleMonitoring()
    {
        if (IsMonitoring)
        {
            _monitor.Stop();
            _sessionTimer?.Stop();
            IsMonitoring = false;
        }
        else
        {
            _processes.Clear();
            _sessionStart = DateTime.UtcNow;
            SessionTime   = "00:00:00";

            _sessionTimer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromSeconds(1) };
            _sessionTimer.Tick += (_, _) =>
                SessionTime = (DateTime.UtcNow - _sessionStart).ToString(@"hh\:mm\:ss");
            _sessionTimer.Start();

            _monitor.Start(TimeSpan.FromSeconds(3));
            IsMonitoring = true;
        }
    }

    private void OnSnapshotReady(IReadOnlyList<ProcessMetricSnapshot> snapshots, SystemSnapshot sys)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            TotalCpuPercent = sys.TotalCpuPercent;
            TotalRamPercent = sys.RamPercent;
            TotalRamLabel   = $"{sys.UsedRamGb:F1} / {sys.TotalRamGb:F1} GB";
            TotalGpuPercent = sys.TotalGpuPercent;

            var existing = _processes.ToDictionary(p => p.Pid);

            foreach (var snap in snapshots)
            {
                if (existing.TryGetValue(snap.Pid, out var info))
                {
                    info.CpuUsage           = snap.CpuUsage;
                    info.CurrentRamMb       = snap.RamMb;
                    info.PeakRamMb          = Math.Max(info.PeakRamMb, snap.PeakRamMb);
                    info.NetworkConnections = snap.NetworkConnections;
                    info.ActiveDuration     = snap.ActiveDuration;
                    info.DiskReadKbps       = snap.DiskReadKbps;
                    info.DiskWriteKbps      = snap.DiskWriteKbps;
                    info.GpuPercent         = snap.GpuPercent;
                    existing.Remove(snap.Pid);
                }
                else
                {
                    _processes.Add(new ProcessInfo
                    {
                        Pid                = snap.Pid,
                        Name               = snap.Name,
                        IsAccessDenied     = snap.IsAccessDenied,
                        CpuUsage           = snap.CpuUsage,
                        CurrentRamMb       = snap.RamMb,
                        PeakRamMb          = snap.PeakRamMb,
                        NetworkConnections = snap.NetworkConnections,
                        ActiveDuration     = snap.ActiveDuration,
                        DiskReadKbps       = snap.DiskReadKbps,
                        DiskWriteKbps      = snap.DiskWriteKbps,
                        GpuPercent         = snap.GpuPercent
                    });
                }
            }

            foreach (var gone in existing.Values.ToList())
                _processes.Remove(gone);

            _filteredView.Refresh();
            FilteredCount = _filteredView.Cast<object>().Count();
        });
    }

    // ── Process Actions ───────────────────────────────────────────────────────
    public void KillSelectedProcess()
    {
        var target = _selectedProcess;
        if (target == null) return;
        try
        {
            Process.GetProcessById(target.Pid).Kill(entireProcessTree: false);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Cannot kill \"{target.Name}\":\n{ex.Message}",
                "Kill Process", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void SetSelectedPriority(string? priorityName)
    {
        var target = _selectedProcess;
        if (target == null || string.IsNullOrEmpty(priorityName)) return;
        try
        {
            var proc = Process.GetProcessById(target.Pid);
            proc.PriorityClass = Enum.Parse<ProcessPriorityClass>(priorityName);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Cannot change priority of \"{target.Name}\":\n{ex.Message}",
                "Set Priority", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void OpenFileLocation()
    {
        var target = _selectedProcess;
        if (target == null) return;
        try
        {
            var proc = Process.GetProcessById(target.Pid);
            string? path = proc.MainModule?.FileName;
            if (!string.IsNullOrEmpty(path))
                Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Cannot open location for \"{target.Name}\":\n{ex.Message}",
                "Open Location", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ── Startup Programs ─────────────────────────────────────────────────────
    public void LoadStartupEntries()
    {
        _startup.Clear();
        try
        {
            foreach (var e in StartupManagerService.GetAllEntries())
                _startup.Add(e);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Cannot load startup entries:\n{ex.Message}",
                "Startup Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ── Filter / Mascot ───────────────────────────────────────────────────────
    private bool FilterProcess(object obj)
    {
        if (obj is not ProcessInfo p) return false;
        return string.IsNullOrWhiteSpace(_searchText)
            || p.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateMascot()
    {
        (MascotLevel, MascotStatusText) = TotalCpuPercent switch
        {
            >= 70 => ("High",   "MAX POWER / OVERCLOCKING!"),
            >= 30 => ("Medium", "PROCESSING / WARMING UP"),
            _     => ("Low",    "SYSTEM STABLE / CHILLING")
        };
    }

    // ── Export ────────────────────────────────────────────────────────────────
    private async void ExportJson()
    {
        var dlg = new SaveFileDialog
        {
            Title      = "Export Session — JSON",
            Filter     = "JSON Files (*.json)|*.json",
            FileName   = $"ProcessAnalyzerPro_{DateTime.Now:yyyyMMdd_HHmmss}.json",
            DefaultExt = ".json"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            await ExportService.ExportJsonAsync(_processes, dlg.FileName);
            MessageBox.Show($"Exported to:\n{dlg.FileName}", "Export Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ExportCsv()
    {
        var dlg = new SaveFileDialog
        {
            Title      = "Export Session — CSV",
            Filter     = "CSV Files (*.csv)|*.csv",
            FileName   = $"ProcessAnalyzerPro_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            DefaultExt = ".csv"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            await ExportService.ExportCsvAsync(_processes, dlg.FileName);
            MessageBox.Show($"Exported to:\n{dlg.FileName}", "Export Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _monitor.SnapshotReady -= OnSnapshotReady;
        _monitor.Dispose();
        _sessionTimer?.Stop();
    }
}

// ── Relay commands ────────────────────────────────────────────────────────────
internal sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? _) => _canExecute?.Invoke() ?? true;
    public void Execute(object? _)    => _execute();
}

internal sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;

    public RelayCommand(Action<T?> execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? _) => true;
    public void Execute(object? parameter) => _execute(parameter is T t ? t : default);
}
