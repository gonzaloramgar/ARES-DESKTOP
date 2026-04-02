using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AresAssistant.Config;
using AresAssistant.Core;
using AresAssistant.ViewModels;
using AresAssistant.Views;
using Newtonsoft.Json.Linq;

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
            if (win is MainWindow mw)
            {
                mw.ReregisterHotkeys();
                mw.ApplyRuntimeConfig();
                break;
            }
        }

        Close();
    }

    private async void CheckOllama_Click(object sender, RoutedEventArgs e)
        => await _vm.CheckOllamaAsync();

    private async void TestModelRouting_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _vm.OllamaStatus = "Generando diagnóstico de routing...";
            var data = await BuildRoutingDiagnosticDataAsync();

            while (true)
            {
                var action = AresMessageBox.ShowRoutingDiagnosticPrompt(
                    data.Report,
                    canInstallMissing: data.MissingRouting.Count > 0,
                    canRepairAll: data.MissingRouting.Count > 0 || data.NeedsVisionModel,
                    title: "ARES — Diagnóstico routing");

                if (action == AresMessageBox.RoutingDiagnosticAction.Copy)
                {
                    try
                    {
                        Clipboard.SetText(data.Report);
                        _vm.OllamaStatus = "Diagnóstico copiado al portapapeles";
                    }
                    catch
                    {
                        _vm.OllamaStatus = "No se pudo copiar el diagnóstico";
                    }
                    continue;
                }

                if (action == AresMessageBox.RoutingDiagnosticAction.InstallMissing)
                {
                    if (!await EnsureOllamaReadyForInstallAsync())
                        return;

                    var summary = await InstallMissingModelsAsync(data.MissingRouting);
                    await _vm.CheckOllamaAsync();
                    data = await BuildRoutingDiagnosticDataAsync();
                    AresMessageBox.Show($"{summary}\n\n{data.Report}", "ARES — Diagnóstico routing");
                    _vm.OllamaStatus = "Diagnóstico de routing actualizado";
                    continue;
                }

                if (action == AresMessageBox.RoutingDiagnosticAction.RepairAll)
                {
                    if (!await EnsureOllamaReadyForInstallAsync())
                        return;

                    var summary = await RepairAllAiAsync(data.MissingRouting, data.PreferredVisionModel);
                    await _vm.CheckOllamaAsync();
                    data = await BuildRoutingDiagnosticDataAsync();
                    AresMessageBox.Show($"{summary}\n\n{data.Report}", "ARES — Reparar todo IA");
                    _vm.OllamaStatus = "Reparación IA completada";
                    continue;
                }

                _vm.OllamaStatus = "Diagnóstico de routing listo";
                break;
            }
        }
        catch (Exception ex)
        {
            _vm.OllamaStatus = "Error en diagnóstico de routing";
            AresMessageBox.Show($"No se pudo generar la vista previa:\n{ex.Message}", "ARES — Error");
        }
    }

    private async Task<(string Report, List<string> MissingRouting, bool NeedsVisionModel, string PreferredVisionModel)> BuildRoutingDiagnosticDataAsync()
    {
        var preview = await _vm.BuildMultiModelRoutingPreviewAsync();
        var missingRouting = await _vm.GetMissingPreferredModelsAsync();

        var preferredVision = string.IsNullOrWhiteSpace(_vm.MultiModelVisionModel)
            ? "moondream:latest"
            : _vm.MultiModelVisionModel;

        var ollama = new OllamaClient();
        var installed = await ollama.GetInstalledModelsAsync();
        var hasAnyVision = installed.Any(ModelRouter.IsLikelyVisionModel);
        var preferredVisionInstalled = installed.Any(m => m.Equals(preferredVision, StringComparison.OrdinalIgnoreCase));
        var needsVisionModel = !hasAnyVision || !preferredVisionInstalled;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(preview);
        sb.AppendLine();
        sb.AppendLine($"Visión (preferido): {preferredVision} {(preferredVisionInstalled ? "✓" : "✗")}");
        sb.AppendLine($"Visión (cualquier modelo multimodal): {(hasAnyVision ? "✓" : "✗")}");
        if (needsVisionModel)
            sb.AppendLine("⚠ Reparar todo IA también instalará/configurará el modelo de visión recomendado.");

        return (sb.ToString().TrimEnd(), missingRouting, needsVisionModel, preferredVision);
    }

    private async Task<bool> EnsureOllamaReadyForInstallAsync()
    {
        var ollama = new OllamaClient();
        if (await ollama.IsAvailableAsync())
            return true;

        _vm.OllamaStatus = "Ollama no responde. Intentando iniciarlo...";
        if (OllamaClient.IsInstalled() && await ollama.TryStartAsync(15))
            return true;

        _vm.OllamaStatus = "Ollama no disponible ✗";
        AresMessageBox.Show(
            "No se pudo conectar con Ollama para instalar faltantes.\nAbre Ollama e inténtalo de nuevo.",
            "ARES — Routing");
        return false;
    }

    private async Task<string> InstallMissingModelsAsync(List<string> missing)
    {
        var ollama = new OllamaClient();
        var targets = missing
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var installedOk = new List<string>();
        var alreadyInstalled = new List<string>();
        var failed = new List<string>();
        string? cancelledAt = null;

        foreach (var model in targets)
        {
            var installed = await ollama.GetInstalledModelsAsync();
            if (installed.Any(n => n.Equals(model, StringComparison.OrdinalIgnoreCase)))
            {
                alreadyInstalled.Add(model);
                continue;
            }

            _vm.OllamaStatus = $"Instalando faltante: {model}...";
            var dl = new ModelDownloadWindow(ollama, model) { Owner = this };
            if (dl.ShowDialog() != true)
            {
                cancelledAt = model;
                break;
            }

            installed = await ollama.GetInstalledModelsAsync();
            if (installed.Any(n => n.Equals(model, StringComparison.OrdinalIgnoreCase)))
                installedOk.Add(model);
            else
                failed.Add(model);
        }

        var sb = new System.Text.StringBuilder();
        if (installedOk.Count > 0)
            sb.AppendLine($"✓ Instalados: {string.Join(", ", installedOk)}");
        if (alreadyInstalled.Count > 0)
            sb.AppendLine($"• Ya estaban instalados: {string.Join(", ", alreadyInstalled)}");
        if (failed.Count > 0)
            sb.AppendLine($"✗ Fallaron: {string.Join(", ", failed)}");
        if (!string.IsNullOrWhiteSpace(cancelledAt))
            sb.AppendLine($"⚠ Instalación cancelada en: {cancelledAt}");
        if (sb.Length == 0)
            sb.AppendLine("No se realizaron cambios en modelos.");

        return sb.ToString().TrimEnd();
    }

    private async Task<string> RepairAllAiAsync(List<string> missingRouting, string preferredVisionModel)
    {
        var parts = new List<string>();

        if (missingRouting.Count > 0)
            parts.Add(await InstallMissingModelsAsync(missingRouting));
        else
            parts.Add("• Routing: sin faltantes.");

        parts.Add(await InstallVisionModelIfNeededAsync(preferredVisionModel));
        return string.Join(Environment.NewLine, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private async Task<string> InstallVisionModelIfNeededAsync(string preferredVisionModel)
    {
        var ollama = new OllamaClient();
        var installed = await ollama.GetInstalledModelsAsync();
        var hasAnyVision = installed.Any(ModelRouter.IsLikelyVisionModel);
        var preferredInstalled = installed.Any(m => m.Equals(preferredVisionModel, StringComparison.OrdinalIgnoreCase));

        if (hasAnyVision && preferredInstalled)
            return "✓ Visión: modelo preferido disponible.";

        _vm.OllamaStatus = $"Instalando visión: {preferredVisionModel}...";
        var dl = new ModelDownloadWindow(ollama, preferredVisionModel) { Owner = this };
        if (dl.ShowDialog() != true)
            return $"⚠ Visión: instalación cancelada ({preferredVisionModel}).";

        installed = await ollama.GetInstalledModelsAsync();
        return installed.Any(m => m.Equals(preferredVisionModel, StringComparison.OrdinalIgnoreCase))
            ? $"✓ Visión: instalado {preferredVisionModel}."
            : $"✗ Visión: no se pudo verificar {preferredVisionModel}.";
    }

    private async void QuickRepairAi_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _vm.OllamaStatus = "Analizando estado IA...";
            var data = await BuildRoutingDiagnosticDataAsync();

            if (data.MissingRouting.Count == 0 && !data.NeedsVisionModel)
            {
                AresMessageBox.Show($"No hay nada que reparar.\n\n{data.Report}", "ARES — Reparar todo IA");
                _vm.OllamaStatus = "Sin reparaciones pendientes";
                return;
            }

            if (!await EnsureOllamaReadyForInstallAsync())
                return;

            var summary = await RepairAllAiAsync(data.MissingRouting, data.PreferredVisionModel);
            await _vm.CheckOllamaAsync();

            _vm.OllamaStatus = "Generando diagnóstico actualizado...";
            var refreshed = await BuildRoutingDiagnosticDataAsync();
            AresMessageBox.Show($"{summary}\n\n{refreshed.Report}", "ARES — Reparar todo IA");
            _vm.OllamaStatus = "Reparación IA completada";
        }
        catch (Exception ex)
        {
            _vm.OllamaStatus = "Error en reparación IA";
            AresMessageBox.Show($"No se pudo completar la reparación:\n{ex.Message}", "ARES — Error");
        }
    }

    private async void TestVision_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var ollama = new OllamaClient();
            var installed = await ollama.GetInstalledModelsAsync();
            var preferredVision = string.IsNullOrWhiteSpace(_vm.MultiModelVisionModel)
                ? "moondream:latest"
                : _vm.MultiModelVisionModel;

            bool hasVisionModel = installed.Any(ModelRouter.IsLikelyVisionModel);
            if (!hasVisionModel)
            {
                var install = AresMessageBox.Show(
                    $"No hay modelo multimodal instalado para visión.\n\n¿Quieres descargar ahora '{preferredVision}'?",
                    "ARES — Modelo de visión",
                    MessageBoxButton.YesNo);

                if (install != MessageBoxResult.Yes)
                {
                    AresMessageBox.Show("Instalación cancelada. La prueba de visión requiere un modelo multimodal local (ej. moondream:latest o llava:7b).", "ARES — Visión");
                    return;
                }

                var dl = new ModelDownloadWindow(ollama, preferredVision) { Owner = this };
                if (dl.ShowDialog() != true)
                {
                    AresMessageBox.Show("No se completó la descarga del modelo de visión.", "ARES — Visión");
                    return;
                }
            }

            var tool = MainWindow.ToolRegistry.Get("analyze_screen");
            if (tool == null)
            {
                AresMessageBox.Show("La herramienta analyze_screen no está disponible.", "ARES — Error");
                return;
            }

            _vm.OllamaStatus = "Probando visión local...";
            var result = await tool.ExecuteAsync(new Dictionary<string, JToken>
            {
                ["question"] = "Describe brevemente qué aplicaciones ves y si notas algún error visible."
            });

            _vm.OllamaStatus = result.Success
                ? "Visión local OK"
                : "Visión local con errores";

            AresMessageBox.Show(result.Message, result.Success ? "ARES — Visión" : "ARES — Error visión");
        }
        catch (Exception ex)
        {
            _vm.OllamaStatus = "Error en prueba de visión";
            AresMessageBox.Show($"No se pudo ejecutar analyze_screen:\n{ex.Message}", "ARES — Error");
        }
    }

    private async void TestVoice_Click(object sender, RoutedEventArgs e)
    {
        var speech = MainWindow.SpeechEngine;
        if (speech == null) return;

        // Wait for warm-up to finish so Piper/Edge are ready on first click
        if (MainWindow.WarmUpTask is { IsCompleted: false } t)
        {
            _vm.TtsEngineStatus = "Preparando motor de voz...";
            await t;
        }

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

    private void ReplayTutorial_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _vm.Save();
            var setup = new SetupWindow(isOnboardingRefresh: true, openSplashOnFinish: false)
            {
                Owner = this
            };

            var ok = setup.ShowDialog() == true;
            if (!ok) return;

            // Rehydrate controls from persisted config in case user changed values in onboarding.
            var reloaded = new SettingsWindow { Owner = Owner };
            reloaded.Show();
            Close();
        }
        catch (Exception ex)
        {
            AresMessageBox.Show($"No se pudo abrir el tutorial:\n{ex.Message}", "ARES — Error");
        }
    }

    private void QuickGuide_Click(object sender, RoutedEventArgs e)
    {
        var text = "ARES — Guía rápida\n\n" +
                   "1) Automatización\n" +
                   "- Abrir/cerrar apps, manejar ventanas, crear/borrar carpetas y ejecutar comandos.\n" +
                   "- Programar tareas diarias y consultar historial de acciones.\n\n" +
                   "2) IA local multimodelo\n" +
                   "- Routing automático según tarea (código, razonamiento, visión).\n" +
                   "- Análisis de pantalla local con modelos multimodales instalados en Ollama.\n\n" +
                   "3) Dashboard HUD\n" +
                   "- Widgets de clima, hora mundial, CPU/RAM, tareas y productividad.\n" +
                   "- Resumen diario con IA para entender en qué apps inviertes el tiempo.\n\n" +
                   "4) Ajustes clave\n" +
                   "- Modo autónomo: permite encadenar acciones sin confirmar cada paso.\n" +
                   "- Contexto de proceso: mejora respuestas según app activa.\n" +
                   "- Keep-alive: controla cuándo descargar modelos de RAM.";

        AresMessageBox.Show(text, "ARES — Guía rápida");
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
