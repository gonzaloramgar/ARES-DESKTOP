using System.Windows;
using System.Windows.Threading;
using System.Text.Json;
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
        Path.Combine(AppPaths.DataDirectory, $"crash_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
    private static readonly string RuntimeActionsLogPath = AppPaths.RuntimeActionsLogFile;
    private static readonly object LogSync = new();

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
            AppPaths.EnsureDataDirectories();

            ConfigManager = new ConfigManager(AppPaths.ConfigFile);
            ThemeEngine.Apply(ConfigManager.Config);

            if (ConfigManager.Config.CloseToTray)
                InitTrayIcon();

            // Show setup only on true first run (or if user reset data).
            if (!ConfigManager.Config.SetupCompleted)
            {
                var setup = new SetupWindow();
                setup.Show();
            }
            else
            {
                if (!string.Equals(ConfigManager.Config.OnboardingVersionSeen, CurrentOnboardingVersion, StringComparison.Ordinal))
                {
                    ConfigManager.Update(c => c with { OnboardingVersionSeen = CurrentOnboardingVersion });
                }

                bool isFirstLaunch = !File.Exists(AppPaths.ToolsFile);
                var splash = new SplashWindow(isFirstLaunch);
                splash.Show();
            }

            // Check for updates silently in the background
            _ = CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            WriteCrash("OnStartup", ex);
            AresMessageBox.Show($"Error al iniciar ARES:\n\n{ex.Message}\n\nRevisa los archivos crash_*.log en:\n{AppPaths.DataDirectory}",
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

    public void ShowTrayNotification(string title, string message)
    {
        if (_trayIcon == null || !ConfigManager.Config.CloseToTray)
            return;

        try
        {
            var text = string.IsNullOrWhiteSpace(message) ? "Tarea completada." : message;
            if (text.Length > 220)
                text = text[..220] + "...";

            _trayIcon.BalloonTipTitle = title;
            _trayIcon.BalloonTipText = text;
            _trayIcon.ShowBalloonTip(3500);
        }
        catch
        {
            // Best-effort notification only.
        }
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrash("UI Thread", e.Exception);
        AresMessageBox.Show($"Error inesperado:\n\n{e.Exception.Message}\n\nRevisa los archivos crash_*.log en:\n{AppPaths.DataDirectory}",
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
            AppPaths.EnsureDataDirectories();
            var details = ex == null
                ? "Exception nula"
                : string.Join(
                    Environment.NewLine + "--- INNER ---" + Environment.NewLine,
                    FlattenExceptions(ex).Select(e =>
                        $"{e.GetType().Name}: {e.Message}{Environment.NewLine}{e.StackTrace}"));

            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{context}]\n" +
                      details + "\n" +
                      new string('-', 60) + "\n";

            lock (LogSync)
            {
                File.AppendAllText(CrashLogPath, msg);
            }
        }
        catch { /* if crash log fails, nothing we can do */ }
    }

    public static void WriteAction(string context, string action, object? data = null, string level = "INFO")
    {
        try
        {
            AppPaths.EnsureDataDirectories();
            var payload = data == null ? "{}" : JsonSerializer.Serialize(data);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [{context}] {action} | {payload}{Environment.NewLine}";

            lock (LogSync)
            {
                File.AppendAllText(RuntimeActionsLogPath, line);
            }
        }
        catch
        {
            // Never throw from logging.
        }
    }

    private static IEnumerable<Exception> FlattenExceptions(Exception ex)
    {
        var current = ex;
        while (current != null)
        {
            yield return current;
            current = current.InnerException;
        }
    }
}
