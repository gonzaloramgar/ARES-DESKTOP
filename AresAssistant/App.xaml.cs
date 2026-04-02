using System.Windows;
using System.Windows.Threading;
using AresAssistant.Config;
using AresAssistant.Core;
using AresAssistant.Views;

namespace AresAssistant;

public partial class App : Application
{
    public const string CurrentOnboardingVersion = "2.0";

    /// <summary>App version displayed in splash and setup screens.</summary>
    public static string AppVersion =>
        $"v{typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.1.0"}";

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

            // First time? Show full setup. On upgrades, show refreshed onboarding/tutorial.
            if (!ConfigManager.Config.SetupCompleted)
            {
                var setup = new SetupWindow();
                setup.Show();
            }
            else if (!string.Equals(ConfigManager.Config.OnboardingVersionSeen, CurrentOnboardingVersion, StringComparison.Ordinal))
            {
                var setup = new SetupWindow(isOnboardingRefresh: true);
                setup.Show();
            }
            else
            {
                bool isFirstLaunch = !File.Exists("data/tools.json");
                var splash = new SplashWindow(isFirstLaunch);
                splash.Show();
            }

            // Check for updates silently in the background
            _ = CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            WriteCrash("OnStartup", ex);
            AresMessageBox.Show($"Error al iniciar ARES:\n\n{ex.Message}\n\nRevisa los archivos crash_*.log en la carpeta data/",
                "ARES — Error");
            Shutdown(1);
        }
    }

    private static async Task CheckForUpdatesAsync()
    {
        try
        {
            var release = await UpdateChecker.CheckAsync();
            if (release == null || !UpdateChecker.IsNewer(release.TagName)) return;

            await Current.Dispatcher.InvokeAsync(() =>
            {
                var win = new UpdateWindow(release);
                win.Show();
            });
        }
        catch
        {
            // No internet or API error — silently ignore
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
        AresMessageBox.Show($"Error inesperado:\n\n{e.Exception.Message}\n\nRevisa los archivos crash_*.log en la carpeta data/",
            "ARES — Error");
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
