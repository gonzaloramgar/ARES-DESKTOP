using System.Windows;
using AresAssistant.Config;
using AresAssistant.Core;
using AresAssistant.ViewModels;

namespace AresAssistant.Views;

public partial class SplashWindow : Window
{
    private readonly bool _isFirstLaunch;
    private readonly SplashViewModel _vm = new();

    public SplashWindow(bool isFirstLaunch)
    {
        InitializeComponent();
        DataContext = _vm;
        _isFirstLaunch = isFirstLaunch;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Always re-scan so new apps / Steam games / desktop shortcuts are detected
            await RunFirstLaunchScanAsync();
            await EnsureRequiredModelsAsync();

            OpenMainWindow();
        }
        catch (Exception ex)
        {
            App.WriteCrash("SplashWindow.OnLoaded", ex);
            UpdateStatus($"Error: {ex.Message}", 0);
            await Task.Delay(3000);
            try { OpenMainWindow(); }
            catch (Exception ex2)
            {
                App.WriteCrash("OpenMainWindow", ex2);
                AresMessageBox.Show(
                    $"Error crítico al iniciar ARES:\n\n{ex2.Message}\n\nRevisa los archivos crash_*.log en la carpeta data/",
                    "ARES — Error");
                Application.Current.Shutdown(1);
            }
        }
    }

    private async Task RunFirstLaunchScanAsync()
    {
        var scanner = new SystemScanner();
        int step = 0;
        var steps = new[] { 10, 30, 50, 65, 75, 90, 100 };

        scanner.StatusChanged += msg =>
        {
            Dispatcher.Invoke(() =>
            {
                _vm.StatusText = msg;
                if (step < steps.Length)
                    _vm.Progress = steps[step++];
            });
        };

        var tools = await scanner.ScanAsync();
        SystemScanner.SaveToJson(tools, "data/tools.json");

        UpdateStatus("ARES listo.", 100);
        await Task.Delay(600);
    }

    private void UpdateStatus(string text, int progress)
    {
        Dispatcher.Invoke(() =>
        {
            _vm.StatusText = text;
            _vm.Progress = progress;
        });
    }

    private void OpenMainWindow()
    {
        var mainWindow = new MainWindow();
        mainWindow.Show();
        Close();
    }

    private async Task EnsureRequiredModelsAsync()
    {
        var cfg = App.ConfigManager.Config;

        // If multi-model is off, no strict model bundle enforcement is needed.
        if (!cfg.MultiModelEnabled)
            return;

        UpdateStatus("Verificando modelos locales...", 96);

        var client = new OllamaClient();
        var ready = await client.IsAvailableAsync();
        if (!ready)
            ready = await client.TryStartAsync(12);

        if (!ready)
        {
            AresMessageBox.Show(
                "Ollama no está disponible al iniciar, no pude verificar modelos.\n\nAbre Ajustes > IA para instalar modelos requeridos.",
                "ARES — Verificación de modelos");
            return;
        }

        var installed = await client.GetInstalledModelsAsync();
        var missing = ModelRouter.GetMissingPreferredModels(cfg, installed);
        if (missing.Count == 0)
            return;

        var installNow = AresMessageBox.Show(
            "Faltan modelos requeridos para multimodelo:\n\n" +
            string.Join("\n", missing.Select(m => $"• {m}")) +
            "\n\n¿Quieres instalarlos ahora?",
            "ARES — Modelos faltantes",
            MessageBoxButton.YesNo);

        if (installNow != MessageBoxResult.Yes)
            return;

        foreach (var model in missing)
        {
            UpdateStatus($"Instalando {model}...", 98);
            var dl = new ModelDownloadWindow(client, model) { Owner = this };
            var ok = dl.ShowDialog() == true;
            if (!ok) break;
        }

        var after = await client.GetInstalledModelsAsync();
        var stillMissing = ModelRouter.GetMissingPreferredModels(cfg, after);
        if (stillMissing.Count > 0)
        {
            AresMessageBox.Show(
                "No se pudieron instalar todos los modelos:\n\n" +
                string.Join("\n", stillMissing.Select(m => $"• {m}")),
                "ARES — Modelos pendientes");
        }
    }
}
