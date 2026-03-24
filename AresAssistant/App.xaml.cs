using System.Windows;
using System.Windows.Threading;
using AresAssistant.Config;
using AresAssistant.Windows;

namespace AresAssistant;

public partial class App : Application
{
    public static ConfigManager ConfigManager { get; private set; } = null!;
    private static readonly string CrashLogPath =
        $"data/crash_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handlers — write crash to file before dying
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainException;
        TaskScheduler.UnobservedTaskException += OnTaskException;

        try
        {
            // Ensure data directories exist
            Directory.CreateDirectory("data");
            Directory.CreateDirectory("data/logs");

            ConfigManager = new ConfigManager("data/config.json");
            ThemeEngine.Apply(ConfigManager.Config);

            bool isFirstLaunch = !File.Exists("data/tools.json");

            var splash = new SplashWindow(isFirstLaunch);
            splash.Show();
        }
        catch (Exception ex)
        {
            WriteCrash("OnStartup", ex);
            MessageBox.Show($"Error al iniciar ARES:\n\n{ex.Message}\n\nRevisa los archivos crash_*.log en la carpeta data/",
                "ARES - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrash("UI Thread", e.Exception);
        MessageBox.Show($"Error inesperado:\n\n{e.Exception.Message}\n\nRevisa los archivos crash_*.log en la carpeta data/",
            "ARES - Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
    {
        WriteCrash("AppDomain", e.ExceptionObject as Exception);
    }

    private static void OnTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrash("Task", e.Exception);
        e.SetObserved();
    }

    public static void WriteCrash(string context, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory("data");
            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{context}]\n" +
                      $"{ex?.GetType().Name}: {ex?.Message}\n" +
                      $"{ex?.StackTrace}\n" +
                      $"Inner: {ex?.InnerException?.Message}\n" +
                      new string('-', 60) + "\n";
            File.AppendAllText(CrashLogPath, msg);
        }
        catch { /* if crash log fails, nothing we can do */ }
    }
}
