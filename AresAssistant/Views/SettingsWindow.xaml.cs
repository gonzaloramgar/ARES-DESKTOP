using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AresAssistant.Config;
using AresAssistant.Core;
using AresAssistant.ViewModels;
using AresAssistant.Views;

namespace AresAssistant.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow()
    {
        InitializeComponent();
        _vm = new SettingsViewModel(App.ConfigManager, new OllamaClient());
        DataContext = _vm;
        Loaded += async (_, _) =>
        {
            await _vm.CheckOllamaAsync();
            await _vm.DetectHardwareAsync();
            _vm.RefreshTtsStatus();
        };
    }

    private void PresetColor_Click(object sender, RoutedEventArgs e)
        => _vm.AccentColor = (string)((FrameworkElement)sender).Tag;

    private void PickColor_Click(object sender, RoutedEventArgs e)
    {
        var picker = new ColorPickerWindow(_vm.AccentColor, this);
        if (picker.ShowDialog() == true)
            _vm.AccentColor = picker.SelectedColor;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => DragMove();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _vm.Save();
        ((App)Application.Current).UpdateTrayIcon();

        // Apply new hotkeys immediately without requiring restart
        foreach (Window win in Application.Current.Windows)
        {
            if (win is MainWindow mw) { mw.ReregisterHotkeys(); break; }
        }

        Close();
    }

    private async void CheckOllama_Click(object sender, RoutedEventArgs e)
        => await _vm.CheckOllamaAsync();

    private void TestVoice_Click(object sender, RoutedEventArgs e)
    {
        var speech = MainWindow.SpeechEngine;
        if (speech == null) return;

        _vm.TtsEngineStatus = "Sintetizando...";

        void OnEngine(string engine)
        {
            speech.EngineUsed -= OnEngine;
            Dispatcher.InvokeAsync(() =>
            {
                var label = engine switch
                {
                    "piper"      => "Motor: Piper neural offline ✓",
                    "edge"       => "Motor: Edge online neural ✓",
                    "local-winrt"=> "⚠ Motor: WinRT local (Edge falló)",
                    "local-sapi" => "⚠ Motor: SAPI local (Edge falló)",
                    _            => $"Motor: {engine}"
                };
                _vm.TtsEngineStatus = label;
            });
        }
        speech.EngineUsed += OnEngine;
        speech.Speak("Hola, soy ARES. La voz del asistente está funcionando correctamente.");
    }

    private async void DownloadPiper_Click(object sender, RoutedEventArgs e)
    {
        var speech = MainWindow.SpeechEngine;
        if (speech == null) return;

        _vm.CanDownloadPiper = false;
        _vm.TtsEngineStatus = "Descargando Piper neural (~60 MB)...";
        try
        {
            await speech.DownloadPiperAsync();
            _vm.RefreshTtsStatus();
        }
        catch (Exception ex)
        {
            _vm.TtsEngineStatus = $"Error al descargar: {ex.Message}";
            _vm.CanDownloadPiper = true;
        }
    }

    private async void Rescan_Click(object sender, RoutedEventArgs e)
    {
        Title = "ARES — Escaneando...";
        try
        {
            var scanner = new SystemScanner();
            scanner.StatusChanged += msg =>
                Dispatcher.Invoke(() => Title = $"ARES — {msg}");

            var tools = await scanner.ScanAsync();
            SystemScanner.SaveToJson(tools, "data/tools.json");
            MainWindow.ToolRegistry.LoadFromJson("data/tools.json");

            Title = "ARES — Ajustes";
            AresMessageBox.Show($"Escaneo completado. {tools.Count} herramientas cargadas.", "ARES");
        }
        catch (Exception ex)
        {
            Title = "ARES — Ajustes";
            AresMessageBox.Show($"Error durante el escaneo:\n{ex.Message}", "ARES — Error");
        }
    }

    private void PurgeData_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PurgeConfirmationDialog(this);
        if (dialog.ShowDialog() != true) return;

        try
        {
            var dataDir = Path.GetFullPath("data");
            if (Directory.Exists(dataDir))
            {
                foreach (var file in Directory.GetFiles(dataDir, "*", SearchOption.AllDirectories))
                    File.Delete(file);
                foreach (var dir in Directory.GetDirectories(dataDir))
                    Directory.Delete(dir, true);
            }

            // Reset config to defaults and re-apply
            var fresh = new Config.AppConfig();
            App.ConfigManager.Save(fresh);
            Config.ThemeEngine.Apply(fresh);

            Close();

            AresMessageBox.Show(
                "Todos los datos han sido eliminados.\nLa aplicación se reiniciará.",
                "ARES — Datos eliminados");

            // Restart the app
            var exePath = Environment.ProcessPath;
            if (exePath != null)
            {
                System.Diagnostics.Process.Start(exePath);
                Application.Current.Shutdown();
            }
        }
        catch (Exception ex)
        {
            AresMessageBox.Show($"Error al eliminar datos:\n{ex.Message}", "ARES — Error");
        }
    }
}
