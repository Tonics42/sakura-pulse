<div align="center">

<img src="Resources/app_icon.ico" width="80" alt="Sakura Pulse icon"/>

# 🌸 Sakura Pulse

**Real-time Windows process monitor with a sakura aesthetic**

[![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-blue?logo=windows)](https://github.com)
[![Framework](https://img.shields.io/badge/.NET-8.0-purple?logo=dotnet)](https://dotnet.microsoft.com)
[![License](https://img.shields.io/badge/license-MIT-pink)](LICENSE)

</div>

---

## ✨ Features

| Category | Details |
|---|---|
| **Process monitoring** | CPU %, RAM, Peak RAM, Disk R/W, GPU %, Network connections, Duration |
| **Live sorting** | Processes automatically sorted by CPU usage, updated every 3 s |
| **Search / filter** | Instant filter by process name |
| **System tray** | Closes to tray — monitoring continues in background |
| **Startup manager** | View, enable/disable, add, or remove Windows startup entries |
| **Export** | Save snapshot as JSON or CSV |
| **Process actions** | Kill process, open file location, change priority — via right-click menu |

## 📸 Screenshot

> Semi-transparent panels over a sakura background with a pastel lavender palette.

## 🚀 Getting Started

### Requirements

- Windows 10 (1903+) or Windows 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) *(x64)*
- Run as **Administrator** for full process metrics (optional but recommended)

### Run from release

1. Download `SakuraPulse_v1.0.zip` from [Releases](../../releases)
2. Extract anywhere
3. Run `ProcessAnalyzerPro.exe`

### Build from source

```bash
git clone https://github.com/artemnokia20/sakura-pulse.git
cd sakura-pulse
dotnet build -c Release
```

Output: `bin\Release\net8.0-windows\ProcessAnalyzerPro.exe`

## 🏗️ Project Structure

```
ProcessAnalyzerPro/
├── Models/
│   ├── ProcessInfo.cs          # Per-process data model (INotifyPropertyChanged)
│   └── StartupEntry.cs         # Startup entry model
├── Services/
│   ├── ProcessMonitorService.cs  # Background polling loop, CPU/Disk deltas via P/Invoke
│   ├── GpuMonitorService.cs      # GPU % per PID via "GPU Engine" perf counters
│   ├── NetworkMonitorService.cs  # TCP connection count per PID via iphlpapi.dll
│   ├── StartupManagerService.cs  # Registry-based startup manager (HKCU + HKLM)
│   └── ExportService.cs          # JSON / CSV export
├── ViewModels/
│   └── MainViewModel.cs        # MVVM glue, commands, filter, mascot logic
├── Converters/
│   └── ValueConverters.cs      # CPU load → brush, Network → brush
├── Themes/
│   └── CyberpunkTheme.xaml     # Full WPF control style library
├── Resources/
│   ├── sakura_bg.jpg           # Background image
│   └── app_icon.ico            # App icon
├── MainWindow.xaml             # Main UI layout
└── MainWindow.xaml.cs          # Code-behind (tray, chrome buttons, startup actions)
```

## ⚙️ Technical Notes

- **GPU monitoring** requires Windows 10 1903+ and a WDDM 2.x driver. Falls back silently.
- **Disk I/O** uses `GetProcessIoCounters` (kernel32.dll) — cumulative delta per tick.
- **Network** uses `GetExtendedTcpTable` (iphlpapi.dll) — active TCP connections per PID.
- **CPU %** is calculated as `ΔcpuTime / Δwall / coreCount × 100`, matching Task Manager.
- Monitoring runs on a background thread pool; all UI updates are dispatched via `Dispatcher.InvokeAsync`.

## 📄 License

MIT © 2025
