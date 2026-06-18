using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using ProcessAnalyzerPro.Models;
using ProcessAnalyzerPro.Services;
using ProcessAnalyzerPro.ViewModels;
using Application = System.Windows.Application;
using MessageBox  = System.Windows.MessageBox;

namespace ProcessAnalyzerPro;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly NotifyIcon    _trayIcon;
    private bool _forceClose;
    private bool _disposed;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
        _trayIcon = BuildTrayIcon();
    }

    // ── Tray ─────────────────────────────────────────────────────────────────
    private NotifyIcon BuildTrayIcon()
    {
        System.Drawing.Icon? icon = null;
        try
        {
            var stream = Application.GetResourceStream(
                new Uri("pack://application:,,,/Resources/app_icon.ico"))?.Stream;
            if (stream != null) icon = new System.Drawing.Icon(stream);
        }
        catch { }

        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть Sakura Pulse", null, (_, _) => ShowFromTray());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => ExitApplication());

        var ni = new NotifyIcon
        {
            Text             = "Sakura Pulse",
            Icon             = icon ?? SystemIcons.Application,
            Visible          = true,
            ContextMenuStrip = menu
        };
        ni.DoubleClick += (_, _) => ShowFromTray();
        return ni;
    }

    private void ShowFromTray() => BringToFront();

    public void BringToFront()
    {
        _vm.ResumeMonitoring();
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _forceClose = true;
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Close();
    }

    // ── Window lifecycle ─────────────────────────────────────────────────────
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_forceClose)
        {
            e.Cancel = true;
            _vm.SuspendMonitoring();
            Hide();
            _trayIcon.ShowBalloonTip(
                2000, "Sakura Pulse",
                "Работает в фоне. Двойной клик по иконке для открытия.",
                ToolTipIcon.Info);
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (!_disposed)
        {
            _disposed = true;
            _vm.Dispose();
        }
        base.OnClosed(e);
    }

    // ── Custom chrome ────────────────────────────────────────────────────────
    private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal : WindowState.Maximized;

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Hide();

    // ── Process context menu ─────────────────────────────────────────────────
    private void KillMenuItem_Click(object sender, RoutedEventArgs e)
        => _vm.KillSelectedProcess();

    private void OpenLocationMenuItem_Click(object sender, RoutedEventArgs e)
        => _vm.OpenFileLocation();

    private void Priority_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string priority)
            _vm.SetSelectedPriority(priority);
    }

    // ── Startup tab ──────────────────────────────────────────────────────────
    private void ToggleBorder_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is StartupEntry entry)
            ToggleEntry(entry);
    }

    private void ToggleEntry(StartupEntry entry)
    {
        try { StartupManagerService.SetEnabled(entry, !entry.IsEnabled); }
        catch (Exception ex)
        {
            MessageBox.Show($"Cannot toggle startup entry:\n{ex.Message}",
                "Sakura Pulse", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RemoveBorder_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is StartupEntry entry)
        {
            var result = MessageBox.Show(
                $"Удалить «{entry.Name}» из автозапуска?",
                "Sakura Pulse", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            try { StartupManagerService.RemoveEntry(entry.Name); _vm.LoadStartupEntries(); }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления:\n{ex.Message}",
                    "Sakura Pulse", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void AddStartupBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            Title  = "Выберите программу для автозапуска"
        };
        if (dlg.ShowDialog() != true) return;

        var path = dlg.FileName;
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        try
        {
            StartupManagerService.AddEntry(name, $"\"{path}\"");
            _vm.LoadStartupEntries();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось добавить в автозапуск:\n{ex.Message}",
                "Sakura Pulse", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
