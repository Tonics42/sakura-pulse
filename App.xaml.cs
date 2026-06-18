using System.Windows;

namespace ProcessAnalyzerPro;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Register ALL handlers before base.OnStartup so nothing slips through
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            string msg = args.ExceptionObject?.ToString() ?? "Unknown fatal error";
            MessageBox.Show(msg, "ProcessAnalyzerPro — Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.ToString(),
                "ProcessAnalyzerPro — Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            MessageBox.Show(args.Exception.ToString(),
                "ProcessAnalyzerPro — Task Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.SetObserved();
        };

        base.OnStartup(e);
    }
}
