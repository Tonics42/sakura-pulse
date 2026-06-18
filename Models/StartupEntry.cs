using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProcessAnalyzerPro.Models;

public sealed class StartupEntry : INotifyPropertyChanged
{
    private bool _isEnabled;

    public string Name    { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty; // "HKCU" | "HKLM"

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(ToggleText));
        }
    }

    public string StatusDisplay => IsEnabled ? "ENABLED" : "DISABLED";
    public string ToggleText    => IsEnabled ? "Disable" : "Enable";

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
