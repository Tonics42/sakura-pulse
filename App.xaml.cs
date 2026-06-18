using System.IO;
using System.Threading;
using System.Windows;

namespace ProcessAnalyzerPro;

public partial class App : Application
{
    private static Mutex? _mutex;
    private const string MutexName = "SakuraPulse_SingleInstance_2025";
    private const string EventName = "SakuraPulse_BringToFront_2025";

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out bool isOwner);
        if (!isOwner)
        {
            // Another instance is running — signal it to show itself and exit
            try { using var h = EventWaitHandle.OpenExisting(EventName); h.Set(); }
            catch { }
            Shutdown();
            return;
        }

        CreateDesktopShortcut();

        // Background thread listens for "show me" signals from future duplicate launches
        var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        new Thread(() =>
        {
            while (showEvent.WaitOne())
                Dispatcher.InvokeAsync(() => (MainWindow as MainWindow)?.BringToFront());
        }) { IsBackground = true }.Start();

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            MessageBox.Show(args.ExceptionObject?.ToString() ?? "Unknown fatal error",
                "Sakura Pulse — Fatal", MessageBoxButton.OK, MessageBoxImage.Error);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.ToString(),
                "Sakura Pulse — Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            MessageBox.Show(args.Exception.ToString(),
                "Sakura Pulse — Task Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.SetObserved();
        };

        base.OnStartup(e);
    }

    private static void CreateDesktopShortcut()
    {
        try
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string lnkPath = Path.Combine(desktop, "Sakura Pulse.lnk");
            if (File.Exists(lnkPath)) return; // already created

            string exe = Environment.ProcessPath
                ?? System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;

            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            dynamic link  = shell.CreateShortcut(lnkPath);
            link.TargetPath       = exe;
            link.WorkingDirectory = Path.GetDirectoryName(exe);
            link.IconLocation     = $"{exe},0";
            link.Description      = "Sakura Pulse — System Monitor";
            link.Save();
        }
        catch { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
