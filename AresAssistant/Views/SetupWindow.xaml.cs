using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using AresAssistant.Config;
using AresAssistant.Core;
using AresAssistant.Helpers;
using Path = System.IO.Path;

namespace AresAssistant.Views;

public partial class SetupWindow : Window
{
    private const int TotalPages = 6;

    private string SelectedModel => _selectedPerfMode switch
    {
        "avanzado" => "qwen2.5:14b",
        "equilibrado" => "qwen2.5:7b",
        _ => "qwen2.5:3b"
    };

    private string SelectedCodingModel => _selectedPerfMode switch
    {
        "avanzado" => "qwen2.5-coder:7b",
        "equilibrado" => "qwen2.5-coder:7b",
        _ => "qwen2.5-coder:3b"
    };

    private string SelectedVisionModel => _selectedPerfMode switch
    {
        "avanzado" => "llava:7b",
        _ => "moondream:latest"
    };
    private int _currentPage;

    private string _selectedColor = "#ff2222";
    private string _selectedPersonality = "formal";
    private string _selectedPerfMode = "ligero";
    private string _selectedVoiceGender = "masculino";

    private Border[] _personalityCards = [];
    private Border[] _perfModeCards = [];
    private Border[] _voiceGenderCards = [];
    private Grid[] _pages = [];
    private Ellipse[] _dots = [];
    private SpeechEngine? _testSpeech;
    private Task? _warmUpTask;
    private readonly bool _isOnboardingRefresh;
    private readonly bool _openSplashOnFinish;

    public SetupWindow(bool isOnboardingRefresh = false, bool openSplashOnFinish = true)
    {
        _isOnboardingRefresh = isOnboardingRefresh;
        _openSplashOnFinish = openSplashOnFinish;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            _personalityCards = [CardFormal, CardCasual, CardSarcastico, CardTecnico];
            _perfModeCards = [CardLigero, CardAvanzado];
            _voiceGenderCards = [CardMasculino, CardFemenino];
            _pages = [Page0, Page4, Page2, Page1, Page5, Page6];
            _dots = [Dot0, Dot1, Dot2, Dot3, Dot4, Dot5];

            ApplyConfigToControls(App.ConfigManager.Config);

            UpdateColorPreview(_selectedColor);
            HighlightPersonalityCard(_selectedPersonality);
            HighlightPerfModeCard(_selectedPerfMode);
            HighlightVoiceGenderCard(_selectedVoiceGender);
            UpdateNavigation();

            // Start warming up Piper + Edge early so the voice test works on first click
            _testSpeech = new SpeechEngine { SkipLocalFallback = true };
            _warmUpTask = _testSpeech.WarmUpAsync();

            // Staggered entrance for the first page content
            await Task.Delay(300);
            AnimatePageEntrance(Page0);

            await DetectHardwareAsync();

            if (_isOnboardingRefresh)
                BtnNext.Content = "CONTINUAR →";
        };
    }

    private void ApplyConfigToControls(AppConfig cfg)
    {
        _selectedColor = cfg.AccentColor;
        _selectedPersonality = cfg.Personality;
        _selectedPerfMode = cfg.PerformanceMode;
        _selectedVoiceGender = cfg.TtsVoiceGender;

        NameBox.Text = string.IsNullOrWhiteSpace(cfg.AssistantName) ? "ARES" : cfg.AssistantName;
        OpacitySlider.Value = cfg.OverlayOpacity;
        HotkeyShowBox.Text = string.IsNullOrWhiteSpace(cfg.ShowHideHotkey) ? "Ctrl+Space" : cfg.ShowHideHotkey;
        HotkeyModeBox.Text = string.IsNullOrWhiteSpace(cfg.ToggleModeHotkey) ? "Ctrl+Shift+Space" : cfg.ToggleModeHotkey;

        ChkLaunchWindows.IsChecked = cfg.LaunchWithWindows;
        ChkSaveChat.IsChecked = cfg.SaveChatHistory;
        ChkCloseToTray.IsChecked = cfg.CloseToTray;
        ChkConfirmation.IsChecked = cfg.ConfirmationAlertsEnabled;
        ChkVoice.IsChecked = cfg.VoiceEnabled;
        SetupVolumeSlider.Value = cfg.TtsVolume;

        ChkWidgetWeather.IsChecked = cfg.WidgetWeatherEnabled;
        ChkWidgetWorldClock.IsChecked = cfg.WidgetWorldClockEnabled;
        ChkWidgetSystemLive.IsChecked = cfg.WidgetSystemLiveEnabled;
        ChkWidgetTasks.IsChecked = cfg.WidgetTasksEnabled;
        ChkWidgetProductivity.IsChecked = cfg.WidgetProductivityEnabled;

        SelectComboByTag(FontSizeBox, cfg.FontSize);
        SelectComboByTag(PositionBox, cfg.OverlayPosition);
        SelectComboByTag(ResponseLengthBox, cfg.ResponseLength);
        SelectComboByTag(KeepAliveBox, cfg.ModelKeepAliveMinutes.ToString());
    }

    private static void SelectComboByTag(System.Windows.Controls.ComboBox combo, string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return;
        foreach (var item in combo.Items)
        {
            if (item is System.Windows.Controls.ComboBoxItem cb && string.Equals(cb.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = cb;
                return;
            }
        }
    }

    // ═══════════════ Navigation ═══════════════

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage < TotalPages - 1)
            GoToPage(_currentPage + 1, forward: true);
        else
            Finish();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 0)
            GoToPage(_currentPage - 1, forward: false);
    }

    private void GoToPage(int target, bool forward)
    {
        if (target < 0 || target >= TotalPages || target == _currentPage) return;

        var outPage = _pages[_currentPage];
        var inPage = _pages[target];

        AnimationHelper.SlideTransition(outPage, inPage, forward);

        _currentPage = target;
        UpdateNavigation();

        // Already warmed up from window load — nothing extra needed here

        // Animate inner sections with stagger
        AnimatePageEntrance(inPage);
    }

    private void UpdateNavigation()
    {
        BtnBack.Visibility = _currentPage > 0 ? Visibility.Visible : Visibility.Collapsed;
        BtnNext.Content = _currentPage == TotalPages - 1 ? "✦  COMENZAR" : "SIGUIENTE →";

        StepLabel.Text = $"Paso {_currentPage + 1} de {TotalPages}";

        // Update dots
        var accent = (SolidColorBrush)FindResource("AccentBrush");

        for (int i = 0; i < _dots.Length; i++)
        {
            bool active = i == _currentPage;
            bool visited = i < _currentPage;

            _dots[i].Fill = active ? accent
                          : visited ? accent
                          : new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x42));

            double targetSize = active ? 10 : 8;
            double targetOpacity = active ? 1.0 : visited ? 0.6 : 0.35;

            var dur = AnimationHelper.Fast;
            _dots[i].BeginAnimation(WidthProperty,
                new DoubleAnimation(targetSize, dur) { EasingFunction = AnimationHelper.EaseOut });
            _dots[i].BeginAnimation(HeightProperty,
                new DoubleAnimation(targetSize, dur) { EasingFunction = AnimationHelper.EaseOut });
            _dots[i].BeginAnimation(OpacityProperty,
                new DoubleAnimation(targetOpacity, dur) { EasingFunction = AnimationHelper.EaseOut });
        }
    }

    private void AnimatePageEntrance(Grid page)
    {
        // Find all Border children (section cards) and animate them with stagger
        int delay = 0;
        foreach (var child in FindVisualChildren<Border>(page))
        {
            // Only animate top-level section borders (direct children of page StackPanel with padding)
            if (child.Parent is StackPanel && child.Padding.Left >= 14)
            {
                AnimationHelper.FadeSlideIn(child, fromY: AnimationHelper.SlideDistanceSmall, delayMs: delay);
                delay += AnimationHelper.IsAdvanced ? 100 : 50;
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) yield return t;
            foreach (var sub in FindVisualChildren<T>(child))
                yield return sub;
        }
    }

    // ═══════════════ Hardware ═══════════════

    private async Task DetectHardwareAsync()
    {
        HardwareInfo hw;
        try
        {
            hw = await Task.Run(HardwareDetector.Detect);
        }
        catch
        {
            HwCpuText.Text = "CPU: no detectado";
            HwRamText.Text = "RAM: no detectada";
            HwRecommendText.Text = "Recomendado: Ligero";
            return;
        }

        HwCpuText.Text = $"CPU: {hw.CpuName} ({hw.CpuCores} hilos)";
        HwRamText.Text = $"RAM: {hw.TotalRamGb:F1} GB";
        HwRecommendText.Text = hw.RecommendedMode == "avanzado"
            ? "⮞ Recomendado: Avanzado"
            : "⮞ Recomendado: Ligero";

        _selectedPerfMode = hw.RecommendedMode;
        HighlightPerfModeCard(_selectedPerfMode);
    }

    // ═══════════════ Color ═══════════════

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        var hex = (string)((FrameworkElement)sender).Tag;
        _selectedColor = hex;
        UpdateColorPreview(hex);

        App.ConfigManager.Update(c => c with { AccentColor = hex });
        ThemeEngine.Apply(App.ConfigManager.Config);
    }

    private void UpdateColorPreview(string hex)
    {
        if (ThemeEngine.TryParseColor(hex, out var color))
        {
            ColorPreview.Background = new SolidColorBrush(color);
            ColorHexText.Text = hex;
        }
    }

    private void Opacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityLabel != null)
            OpacityLabel.Text = $"{e.NewValue:P0}";
    }

    // ═══════════════ Personality ═══════════════

    private void Personality_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border card && card.Tag is string personality)
        {
            _selectedPersonality = personality;
            HighlightPersonalityCard(personality);
            AnimationHelper.BounceSelect(card);
        }
    }

    private void HighlightPersonalityCard(string personality)
    {
        var accent = (SolidColorBrush)FindResource("AccentBrush");
        var unselected = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e));

        foreach (var card in _personalityCards)
        {
            bool isCurrent = (string)card.Tag == personality;
            card.BorderBrush = isCurrent ? accent : unselected;
            card.Background = isCurrent
                ? new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x16))
                : new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11));
        }
    }

    // ═══════════════ Performance Mode ═══════════════

    private void PerfMode_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border card && card.Tag is string mode)
        {
            _selectedPerfMode = mode;
            HighlightPerfModeCard(mode);
            AnimationHelper.BounceSelect(card);


        }
    }

    private void HighlightPerfModeCard(string mode)
    {
        var accent = (SolidColorBrush)FindResource("AccentBrush");
        var unselected = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e));

        foreach (var card in _perfModeCards)
        {
            bool isCurrent = (string)card.Tag == mode;
            card.BorderBrush = isCurrent ? accent : unselected;
            card.Background = isCurrent
                ? new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x16))
                : new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11));
        }
    }

    // ═══════════════ Voice Gender ═══════════════

    private void VoiceGender_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border card && card.Tag is string gender)
        {
            _selectedVoiceGender = gender;
            HighlightVoiceGenderCard(gender);
            AnimationHelper.BounceSelect(card);
        }
    }

    private void HighlightVoiceGenderCard(string gender)
    {
        var accent = (SolidColorBrush)FindResource("AccentBrush");
        var unselected = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e));

        foreach (var card in _voiceGenderCards)
        {
            bool isCurrent = (string)card.Tag == gender;
            card.BorderBrush = isCurrent ? accent : unselected;
            card.Background = isCurrent
                ? new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x16))
                : new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11));
        }
    }

    private void SetupVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SetupVolumeLabel != null)
            SetupVolumeLabel.Text = $"{e.NewValue:P0}";
    }

    private async void SetupTestVoice_Click(object sender, RoutedEventArgs e)
    {
        _testSpeech ??= new SpeechEngine { SkipLocalFallback = true };
        _testSpeech.Enabled = true;
        _testSpeech.Volume = (float)SetupVolumeSlider.Value;
        _testSpeech.VoiceGender = _selectedVoiceGender;

        // Wait for warm-up to finish so Piper/Edge are ready
        if (_warmUpTask != null)
        {
            SetupTestVoice.IsEnabled = false;
            SetupTestVoice.Content = "⏳  Preparando...";
            await _warmUpTask;
            _warmUpTask = null;
            SetupTestVoice.IsEnabled = true;
            SetupTestVoice.Content = "▶  Probar voz";
        }

        _testSpeech.Speak("Hola, soy ARES. Esta es una prueba de voz.");
    }

    // ═══════════════ Ollama Auto-Install ═══════════════

    private async void SetupInstallOllama_Click(object sender, RoutedEventArgs e)
    {
        SetupInstallOllama.IsEnabled = false;
        OllamaProgressBorder.Visibility = Visibility.Visible;

        var model = SelectedModel;
        var requiredModels = new[]
        {
            model,
            SelectedCodingModel,
            SelectedVisionModel
        }
        .Where(m => !string.IsNullOrWhiteSpace(m))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

        try
        {
            var client = new OllamaClient();

            // Step 1: Check if Ollama is already running
            SetStatus("Comprobando Ollama...");
            SetProgress(0.02);
            bool ollamaReady = await client.IsAvailableAsync();

            if (!ollamaReady)
            {
                // Step 1b: Try to start Ollama if installed but not running
                if (OllamaClient.IsInstalled())
                {
                    SetStatus("Iniciando Ollama...");
                    SetProgress(0.05);
                    ollamaReady = await client.TryStartAsync(15);
                }

                if (!ollamaReady)
                {
                    // Step 2: Not installed — show installer window
                    SetStatus("Instalando Ollama...");
                    var installWindow = new OllamaInstallWindow { Owner = this };
                    var installResult = installWindow.ShowDialog();

                    if (installResult != true)
                    {
                        SetStatus("Instalación cancelada");
                        SetupInstallOllama.IsEnabled = true;
                        OllamaProgressBorder.Visibility = Visibility.Collapsed;
                        return;
                    }
                    ollamaReady = true;
                }
            }

            // Step 5: Ensure all required models are installed
            var installed = await client.GetInstalledModelsAsync();
            for (int i = 0; i < requiredModels.Count; i++)
            {
                var req = requiredModels[i];
                var stepStart = 0.5 + (i * 0.45 / requiredModels.Count);
                var stepEnd = 0.5 + ((i + 1) * 0.45 / requiredModels.Count);
                SetProgress(stepStart);

                bool exists = installed.Any(n => n.Equals(req, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    SetStatus($"Descargando {req} ({i + 1}/{requiredModels.Count})...");
                    var dlWindow = new ModelDownloadWindow(client, req) { Owner = this };
                    var dlResult = dlWindow.ShowDialog();

                    if (dlResult != true)
                    {
                        SetStatus("Descarga cancelada");
                        SetupInstallOllama.IsEnabled = true;
                        OllamaProgressBorder.Visibility = Visibility.Collapsed;
                        return;
                    }

                    installed = await client.GetInstalledModelsAsync();
                    exists = installed.Any(n => n.Equals(req, StringComparison.OrdinalIgnoreCase));
                    if (!exists)
                    {
                        SetStatus($"Error: {req} no quedó instalado correctamente.");
                        SetupInstallOllama.IsEnabled = true;
                        OllamaProgressBorder.Visibility = Visibility.Collapsed;
                        return;
                    }
                }

                SetProgress(stepEnd);
            }

            // Done
            SetProgress(1.0);
            Dispatcher.Invoke(() => SetProgress(1.0));
            SetStatus($"✓ Ollama + modelos listos ({string.Join(", ", requiredModels)})");
            OllamaInstallStatus.Foreground = (SolidColorBrush)FindResource("AccentBrush");
            SetupInstallOllama.Content = "✓  Instalado";


        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SetStatus($"Error: {ex.Message}");
            OllamaInstallStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0x44, 0x44));
            SetupInstallOllama.Content = "⬇  Instalar todo";
            SetupInstallOllama.IsEnabled = true;
        }

        void SetStatus(string text) => OllamaInstallStatus.Text = text;

        void SetProgress(double pct)
        {
            pct = Math.Clamp(pct, 0, 1);
            OllamaProgressBorder.UpdateLayout();
            var totalWidth = Math.Max(0, OllamaProgressBorder.ActualWidth - 2);
            OllamaProgressFill.Width = totalWidth * pct;
        }
    }

    private async void SetupDownloadPiper_Click(object sender, RoutedEventArgs e)
    {
        SetupDownloadPiper.IsEnabled = false;
        SetupDownloadPiper.Content = "⏳  Descargando...";
        PiperDownloadStatus.Text = "Descargando voces Piper...";

        try
        {
            var speech = new SpeechEngine();
            await speech.DownloadPiperAsync();
            speech.Dispose();

            PiperDownloadStatus.Text = "✓ Voces offline instaladas";
            PiperDownloadStatus.Foreground = (SolidColorBrush)FindResource("AccentBrush");
            SetupDownloadPiper.Content = "✓  Descargado";
        }
        catch
        {
            PiperDownloadStatus.Text = "Error — reintenta más tarde";
            PiperDownloadStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0x44, 0x44));
            SetupDownloadPiper.Content = "⬇  Descargar voces offline";
            SetupDownloadPiper.IsEnabled = true;
        }
    }

    // ═══════════════ Finish ═══════════════

    private void Finish()
    {
        var name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) name = "ARES";

        var fontSize = (FontSizeBox.SelectedValue as string) ?? "medium";
        var position = (PositionBox.SelectedValue as string) ?? "bottom-right";
        var responseLength = (ResponseLengthBox.SelectedValue as string) ?? "normal";
        var keepAlive = int.TryParse(KeepAliveBox.SelectedValue as string, out var ka) ? ka : 5;

        App.ConfigManager.Update(c => c with
        {
            AccentColor = _selectedColor,
            Personality = _selectedPersonality,
            PerformanceMode = _selectedPerfMode,
            AssistantName = name,
            OllamaModel = SelectedModel,
            MultiModelReasoningModel = SelectedModel,
            MultiModelCodingModel = SelectedCodingModel,
            MultiModelVisionModel = SelectedVisionModel,
            MultiModelFallbacks = _selectedPerfMode == "avanzado"
                ? "qwen2.5:7b,qwen2.5-coder:7b,llava:7b"
                : "qwen2.5:3b,qwen2.5-coder:3b,moondream:latest",
            FontSize = fontSize,
            OverlayOpacity = OpacitySlider.Value,
            OverlayPosition = position,
            ResponseLength = responseLength,
            ShowHideHotkey = HotkeyShowBox.Text?.Trim() is { Length: > 0 } h1 ? h1 : "Ctrl+Space",
            ToggleModeHotkey = HotkeyModeBox.Text?.Trim() is { Length: > 0 } h2 ? h2 : "Ctrl+Shift+Space",
            LaunchWithWindows = ChkLaunchWindows.IsChecked == true,
            SaveChatHistory = ChkSaveChat.IsChecked == true,
            CloseToTray = ChkCloseToTray.IsChecked == true,
            ConfirmationAlertsEnabled = ChkConfirmation.IsChecked == true,
            VoiceEnabled = ChkVoice.IsChecked == true,
            TtsVolume = (float)SetupVolumeSlider.Value,
            TtsVoiceGender = _selectedVoiceGender,
            WidgetWeatherEnabled = ChkWidgetWeather.IsChecked == true,
            WidgetWorldClockEnabled = ChkWidgetWorldClock.IsChecked == true,
            WidgetSystemLiveEnabled = ChkWidgetSystemLive.IsChecked == true,
            WidgetTasksEnabled = ChkWidgetTasks.IsChecked == true,
            WidgetProductivityEnabled = ChkWidgetProductivity.IsChecked == true,
            ModelKeepAliveMinutes = keepAlive,
            SetupCompleted = true,
            OnboardingVersionSeen = App.CurrentOnboardingVersion
        });
        ThemeEngine.Apply(App.ConfigManager.Config);

        if (App.ConfigManager.Config.LaunchWithWindows)
            StartupManager.SetEnabled(true);
        else
            StartupManager.SetEnabled(false);

        _testSpeech?.Stop();
        _testSpeech?.Dispose();
        _testSpeech = null;

        if (_openSplashOnFinish)
        {
            var splash = new SplashWindow(isFirstLaunch: !File.Exists("data/tools.json"));
            splash.Show();
            Close();
            return;
        }

        if (Owner is not null)
            DialogResult = true;
        else
            Close();
    }
}
