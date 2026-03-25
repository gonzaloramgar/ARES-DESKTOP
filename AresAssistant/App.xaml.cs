using System.Windows;
using System.Windows.Threading;
using AresAssistant.Config;
using AresAssistant.Views;

namespace AresAssistant;

public partial class App : Application
{
    public static ConfigManager ConfigManager { get; private set; } = null!;
    public static bool IsExiting { get; set; } = false;

    private System.Windows.Forms.NotifyIcon? _trayIcon;

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

            if (ConfigManager.Config.CloseToTray)
                InitTrayIcon();

            // First time? Show the onboarding setup screen
            if (!ConfigManager.Config.SetupCompleted)
            {
                var setup = new SetupWindow();
                setup.Show();
            }
            else
            {
                bool isFirstLaunch = !File.Exists("data/tools.json");
                var splash = new SplashWindow(isFirstLaunch);
                splash.Show();
            }
        }
        catch (Exception ex)
        {
            WriteCrash("OnStartup", ex);
            MessageBox.Show($"Error al iniciar ARES:\n\n{ex.Message}\n\nRevisa los archivos crash_*.log en la carpeta data/",
                "ARES - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void InitTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "ARES — Asistente activo",
            Visible = true
        };

        try
        {
            _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName);
        }
        catch
        {
            _trayIcon.Icon = System.Drawing.SystemIcons.Application;
        }

        var openItem = new System.Windows.Forms.ToolStripMenuItem("Abrir ARES");
        openItem.Click += (_, _) => ShowMainWindowFromTray();

        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Salir");
        exitItem.Click += (_, _) => ExitFromTray();

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(openItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowMainWindowFromTray();
    }

    private void ShowMainWindowFromTray()
    {
        Dispatcher.Invoke(() =>
        {
            foreach (Window win in Windows)
            {
                if (win is MainWindow mw)
                {
                    mw.Show();
                    mw.Activate();
                    return;
                }
            }
        });
    }

    // Called from tray "Salir" (WinForms thread)
    private void ExitFromTray()
    {
        IsExiting = true;
        CleanupTray();
        Dispatcher.BeginInvoke(Shutdown);
    }

    // Called from SettingsWindow when CloseToTray setting changes at runtime
    public void UpdateTrayIcon()
    {
        if (ConfigManager.Config.CloseToTray)
        {
            if (_trayIcon == null)
                InitTrayIcon();
        }
        else
        {
            CleanupTray();
        }
    }

    // Called from MainWindow.OnClosed (UI thread) when CloseToTray=false
    public void CleanupTray()
    {
        if (_trayIcon == null) return;
        try
        {
            _trayIcon.Visible = false;
            _trayIcon.ContextMenuStrip?.Dispose();
            _trayIcon.Dispose();
        }
        catch { /* ignore cleanup errors */ }
        finally
        {
            _trayIcon = null;
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
