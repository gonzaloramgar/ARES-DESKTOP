using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media;
using AresAssistant.Config;
using AresAssistant.Core;
using AresAssistant.Tools;
using AresAssistant.ViewModels;

namespace AresAssistant.Views;

public partial class MainWindow : Window
{
    private readonly GlobalHotkeyManager _hotkeyManager = new();
    private readonly MainViewModel _vm = new();
    private bool _isVisible = true;
    private readonly List<int> _hotkeyIds = new();

    // Inactivity timer — unloads the model from Ollama RAM after idle period
    private readonly DispatcherTimer _idleTimer;
    private readonly DispatcherTimer _clipboardHintTimer;
    private OllamaClient? _ollamaClient;
    private SchedulerService? _scheduler;
    private ClipboardMonitor? _clipboardMonitor;
    private Action<string, string>? _clipboardHintHandler;
    private ProductivityTracker? _productivityTracker;
    private LocalApiServer? _localApiServer;
    private bool _servicesInitialized;
    private bool _deferredServicesStarted;
    private bool _isClosed;

    public static ChatViewModel ChatViewModel { get; private set; } = null!;
    public static AgentLoop AgentLoop { get; private set; } = null!;
    public static ToolRegistry ToolRegistry { get; private set; } = null!;
    public static PermissionManager PermissionManager { get; private set; } = null!;
    public static PersistentMemoryStore MemoryStore { get; private set; } = null!;
    public static ScheduledTaskStore ScheduledTaskStore { get; private set; } = null!;
    public static SpeechEngine SpeechEngine { get; private set; } = null!;
    public static ProductivityTracker ProductivityTracker { get; private set; } = null!;
    public static ReliabilityTelemetryStore ReliabilityTelemetry { get; private set; } = null!;
    public static SecurityPolicyStore SecurityPolicyStore { get; private set; } = null!;
    public static Task? WarmUpTask { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        _vm.IsInitializingModules = false;
        _vm.InitializationStatus = string.Empty;

        _idleTimer = new DispatcherTimer();
        _idleTimer.Tick += IdleTimer_Tick;
        _clipboardHintTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _clipboardHintTimer.Tick += ClipboardHintTimer_Tick;

        PositionWindow();

        Loaded += OnLoaded;
    }

    private async Task BuildServicesAsync()
    {
        var config = App.ConfigManager.Config;
        AppPaths.EnsureDataDirectories();

        var ollamaClient = new OllamaClient();
        _ollamaClient = ollamaClient;

        // Auto-start Ollama if installed but not running
        _ = ollamaClient.TryStartAsync(10);

        var history = new ConversationHistory();
        var registry = new ToolRegistry();
        var securityPolicyStore = new SecurityPolicyStore(AppPaths.SecurityPolicyFile);
        var permManager = new PermissionManager(config.SecurityPolicyEnabled ? securityPolicyStore : null)
        {
            AutoApproveConfirmations = config.AutonomousMode
        };
        var memoryStore = new PersistentMemoryStore(AppPaths.MemoryFile);
        var processContextProvider = new ProcessContextProvider();
        var scheduledStore = new ScheduledTaskStore(AppPaths.ScheduledTasksFile);
        var productivityTracker = new ProductivityTracker(AppPaths.ProductivityFile);
        var telemetryStore = new ReliabilityTelemetryStore(AppPaths.ReliabilityTelemetryFile, config.ReliabilityTelemetryEnabled);
        var logger = new ActionLogger(AppPaths.LogsDirectory);
        var dispatcher = new ToolDispatcher(registry, permManager, logger, telemetryStore);

        // Register built-in tools
        registry.Register(new CloseAppTool());
        registry.Register(new ScreenshotTool());
        registry.Register(new AnalyzeScreenTool(ollamaClient, App.ConfigManager));
        registry.Register(new ModelBenchmarkTool(ollamaClient, App.ConfigManager));
        registry.Register(new ReadFileTool());
        registry.Register(new WriteFileTool());
        registry.Register(new RunCommandTool());
        registry.Register(new SearchWebTool());
        registry.Register(new ClipboardReadTool());
        registry.Register(new ClipboardWriteTool());
        registry.Register(new VolumeTool());
        registry.Register(new SystemInfoTool());
        registry.Register(new ListWindowsTool());
        registry.Register(new MinimizeWindowTool());
        registry.Register(new MaximizeWindowTool());
        registry.Register(new TypeTextTool());
        registry.Register(new CreateFolderTool());
        registry.Register(new DeleteFolderTool());
        registry.Register(new RecycleBinTool());
        registry.Register(new RememberAppTool(registry));
        registry.Register(new LocationTool());
        registry.Register(new WeatherTool());
        registry.Register(new ActionHistoryTool());
        registry.Register(new ScheduleAddTool(scheduledStore));
        registry.Register(new ScheduleListTool(scheduledStore));
        registry.Register(new ScheduleRemoveTool(scheduledStore));
        registry.Register(new ScheduleSimulateTool(scheduledStore, permManager));
        registry.Register(new MemoryWriteTool(memoryStore));
        registry.Register(new MemoryReadTool(memoryStore));
        registry.Register(new MemoryForgetTool(memoryStore));

        await Dispatcher.Yield(DispatcherPriority.Background);

        if (config.PluginToolsEnabled)
        {
            var pluginLoader = new PluginToolLoader();
            pluginLoader.LoadIntoRegistry(AppPaths.PluginsDirectory, registry);
        }

        // Load auto-generated tools from scan (also loads data/custom-apps.json)
        registry.LoadFromJson(AppPaths.ToolsFile);

        // Hook confirmation dialog
        dispatcher.ConfirmationRequested += ShowConfirmationDialogAsync;

        // Load history if persistence enabled, then strip stale tool-failure messages
        // so old "app not found" results don't prevent the model from retrying.
        if (config.SaveChatHistory && System.IO.File.Exists(AppPaths.ChatHistoryFile))
        {
            history.LoadFromJson(AppPaths.ChatHistoryFile);
            history.PurgeToolFailures();
        }

        await Dispatcher.Yield(DispatcherPriority.Background);

        var agentLoop = new AgentLoop(ollamaClient, history, registry, dispatcher, config, telemetryStore, memoryStore, processContextProvider);
        _ = agentLoop.WarmUpAsync(); // Pre-load model into RAM to eliminate cold-start delay

        var speech = new SpeechEngine { Enabled = config.VoiceEnabled, Volume = config.TtsVolume, VoiceGender = config.TtsVoiceGender, SkipLocalFallback = true };
        WarmUpTask = speech.WarmUpAsync();  // Pre-warm Piper + Edge TTS so first response uses neural voice
        SpeechEngine = speech;

        ToolRegistry = registry;
        PermissionManager = permManager;
        MemoryStore = memoryStore;
        ScheduledTaskStore = scheduledStore;
        ProductivityTracker = productivityTracker;
        ReliabilityTelemetry = telemetryStore;
        SecurityPolicyStore = securityPolicyStore;
        AgentLoop = agentLoop;
        ChatViewModel = new ChatViewModel(agentLoop, history, config, registry, speech, scheduledStore, productivityTracker, ollamaClient, processContextProvider);

        OverlayControl.DataContext = ChatViewModel;
        FullHudControl.DataContext = ChatViewModel;

        await Dispatcher.Yield(DispatcherPriority.Background);

        // Reset inactivity timer every time the agent produces a response
        AgentLoop.ResponseReceived += OnAgentResponseReceived;
        ResetIdleTimer();

        _scheduler = new SchedulerService(scheduledStore, async task =>
        {
            await dispatcher.ExecuteAsync("run_command", new Dictionary<string, Newtonsoft.Json.Linq.JToken>
            {
                ["command"] = task.Command
            }).ConfigureAwait(false);
        })
        {
            Enabled = config.ScheduledAutomationsEnabled
        };
        _scheduler.Start();

        _clipboardMonitor = new ClipboardMonitor
        {
            Enabled = config.ClipboardSmartEnabled
        };
        _clipboardHintHandler = (_, hint) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (ChatViewModel.IsBusy) return;
                ChatViewModel.StatusText = hint;

                _clipboardHintTimer.Stop();
                _clipboardHintTimer.Start();
            });
        };
        _clipboardMonitor.ClipboardSmartHint += _clipboardHintHandler;

        _productivityTracker = productivityTracker;

        // Non-critical services are started after first frame so the main UI appears sooner.
        _ = Dispatcher.BeginInvoke(new Action(StartDeferredServices), DispatcherPriority.ContextIdle);
    }

    private void StartDeferredServices()
    {
        if (_deferredServicesStarted || _isClosed)
            return;

        _deferredServicesStarted = true;
        App.WriteAction("MainWindow", "DeferredServices.Start");
        try
        {
            _clipboardMonitor?.Start();
            _productivityTracker?.Start();

            var cfg = App.ConfigManager.Config;
            if (cfg.LocalApiEnabled)
            {
                _localApiServer = new LocalApiServer(cfg.LocalApiPort, App.ConfigManager, ToolRegistry);
                _localApiServer.Start();
            }

            App.WriteAction("MainWindow", "DeferredServices.Ready", new { localApi = cfg.LocalApiEnabled });
        }
        catch (Exception ex)
        {
            App.WriteCrash("MainWindow.StartDeferredServices", ex);
            App.WriteAction("MainWindow", "DeferredServices.Error", new { ex.Message }, "ERROR");
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_servicesInitialized)
        {
            _servicesInitialized = true;
            _ = InitializeServicesAsync();
        }

        var hwnd = new WindowInteropHelper(this).Handle;
        _hotkeyManager.Initialize(hwnd);

        RegisterHotkeys();
    }

    private async Task InitializeServicesAsync()
    {
        try
        {
            App.WriteAction("MainWindow", "InitializeServices.Start");
            // Let the window render fully before expensive setup.
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            await BuildServicesAsync();
            PositionWindow();
            App.WriteAction("MainWindow", "InitializeServices.Ready");
        }
        catch (Exception ex)
        {
            App.WriteCrash("MainWindow.BuildServices", ex);
            App.WriteAction("MainWindow", "InitializeServices.Error", new { ex.Message }, "ERROR");
            AresMessageBox.Show($"Error al inicializar servicios:\n{ex.Message}", "ARES — Error");
        }
    }

    private void RegisterHotkeys()
    {
        var cfg = App.ConfigManager.Config;

        try
        {
            var (mods1, key1) = GlobalHotkeyManager.ParseHotkey(cfg.ShowHideHotkey);
            _hotkeyIds.Add(_hotkeyManager.Register(mods1, key1, ToggleVisibility));
        }
        catch { /* ignore invalid hotkey string */ }

        try
        {
            var (mods2, key2) = GlobalHotkeyManager.ParseHotkey(cfg.ToggleModeHotkey);
            _hotkeyIds.Add(_hotkeyManager.Register(mods2, key2, ToggleMode));
        }
        catch { /* ignore */ }
    }

    public void ReregisterHotkeys()
    {
        foreach (var id in _hotkeyIds)
            _hotkeyManager.Unregister(id);
        _hotkeyIds.Clear();
        RegisterHotkeys();
    }

    public void ApplyRuntimeConfig()
    {
        var cfg = App.ConfigManager.Config;
        if (PermissionManager != null)
            PermissionManager.AutoApproveConfirmations = cfg.AutonomousMode;
        if (_scheduler != null)
            _scheduler.Enabled = cfg.ScheduledAutomationsEnabled;
        if (_clipboardMonitor != null)
            _clipboardMonitor.Enabled = cfg.ClipboardSmartEnabled;
        if (ReliabilityTelemetry != null)
            ReliabilityTelemetry.Enabled = cfg.ReliabilityTelemetryEnabled;
        ChatViewModel?.RefreshRuntimeConfig(cfg);
    }

    private void ToggleVisibility()
    {
        _isVisible = !_isVisible;
        App.WriteAction("MainWindow", "ToggleVisibility", new { visible = _isVisible });
        if (_isVisible)
            Show();
        else
            Hide();
    }

    public void ToggleMode()
    {
        bool toFullHud = OverlayControl.Visibility == Visibility.Visible;
        App.WriteAction("MainWindow", "ToggleMode", new { toFullHud });
        var fromControl = toFullHud ? (FrameworkElement)OverlayControl : FullHudControl;
        var toControl = toFullHud ? (FrameworkElement)FullHudControl : OverlayControl;

        toControl.Opacity = 0;
        toControl.Visibility = Visibility.Visible;

        if (toFullHud)
        {
            AnimateSizeChange(1200, 800);
        }
        else
        {
            AnimateSizeChange(380, 600);
        }

        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(170));
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
        fadeOut.Completed += (_, _) =>
        {
            fromControl.Visibility = Visibility.Collapsed;
            fromControl.Opacity = 1;
        };

        fromControl.BeginAnimation(OpacityProperty, fadeOut);
        toControl.BeginAnimation(OpacityProperty, fadeIn);

        _vm.ToggleMode();
        PositionWindow();
    }

    private void AnimateSizeChange(double targetWidth, double targetHeight)
    {
        var widthAnim = new DoubleAnimation(targetWidth, TimeSpan.FromMilliseconds(250));
        var heightAnim = new DoubleAnimation(targetHeight, TimeSpan.FromMilliseconds(250));
        BeginAnimation(WidthProperty, widthAnim);
        BeginAnimation(HeightProperty, heightAnim);
    }

    private void PositionWindow()
    {
        var cfg = App.ConfigManager.Config;
        var screen = SystemParameters.WorkArea;

        const double margin = 12;
        var w = _vm.IsOverlayMode ? 380 : 1200;
        var h = _vm.IsOverlayMode ? 600 : 800;

        (Left, Top) = cfg.OverlayPosition switch
        {
            "bottom-left" => (margin, screen.Bottom - h - margin),
            "top-right" => (screen.Right - w - margin, margin),
            "top-left" => (margin, margin),
            _ => (screen.Right - w - margin, screen.Bottom - h - margin) // bottom-right default
        };

        Width = w;
        Height = h;
    }

    private async Task<bool> ShowConfirmationDialogAsync(string toolName, Dictionary<string, Newtonsoft.Json.Linq.JToken> args)
    {
        if (!App.ConfigManager.Config.ConfirmationAlertsEnabled)
            return true;

        bool result = false;
        await Dispatcher.InvokeAsync(() =>
        {
            var dialog = new ConfirmationDialog(toolName, args) { Owner = this };
            result = dialog.ShowDialog() == true;
        });
        return result;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (App.ConfigManager.Config.CloseToTray && !App.IsExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        AgentLoop.ResponseReceived -= OnAgentResponseReceived;
        _idleTimer.Stop();
        _idleTimer.Tick -= IdleTimer_Tick;
        _scheduler?.Stop();
        if (_clipboardMonitor != null && _clipboardHintHandler != null)
            _clipboardMonitor.ClipboardSmartHint -= _clipboardHintHandler;
        _clipboardMonitor?.Stop();
        _productivityTracker?.Stop();
        _localApiServer?.Stop();
        _clipboardHintTimer.Stop();
        _clipboardHintTimer.Tick -= ClipboardHintTimer_Tick;
        // Unload the model so Ollama releases RAM when ARES exits
        _ = _ollamaClient?.UnloadModelAsync(App.ConfigManager.Config.OllamaModel);
        _hotkeyManager.Dispose();
        SpeechEngine?.Dispose();
        base.OnClosed(e);
        ((App)Application.Current).CleanupTray();
        Application.Current.Shutdown();
    }

    private void MainWindow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        // Borderless window: allow dragging from the top strip like a normal title bar.
        // Keep buttons/textboxes clickable by skipping interactive elements.
        var p = e.GetPosition(this);
        if (p.Y > 54)
            return;

        if (e.OriginalSource is DependencyObject d && IsInteractiveTopElement(d))
            return;

        try
        {
            DragMove();
            e.Handled = true;
        }
        catch
        {
            // DragMove can throw if mouse state changes mid-drag; ignore safely.
        }
    }

    private static bool IsInteractiveTopElement(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase
                or System.Windows.Controls.Primitives.TextBoxBase
                or System.Windows.Controls.PasswordBox
                or System.Windows.Controls.ComboBox
                or System.Windows.Controls.Slider
                or System.Windows.Controls.Primitives.ScrollBar
                or System.Windows.Controls.ListBoxItem
                or System.Windows.Controls.Primitives.DataGridColumnHeader)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void ResetIdleTimer()
    {
        var minutes = App.ConfigManager.Config.ModelKeepAliveMinutes;
        if (minutes <= 0)
        {
            _idleTimer.Stop();
            return;
        }

        _idleTimer.Stop();
        _idleTimer.Interval = TimeSpan.FromMinutes(minutes);
        _idleTimer.Start();
    }

    private void OnAgentResponseReceived(string _)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ResetIdleTimer();

            if (!IsVisible && ChatViewModel.Messages.Count > 0)
            {
                var lastAssistant = ChatViewModel.Messages
                    .LastOrDefault(m => m.IsAssistant && !string.IsNullOrWhiteSpace(m.Content));

                if (lastAssistant != null)
                    ((App)Application.Current).ShowTrayNotification("ARES terminó una tarea", lastAssistant.Content);
            }
        }));
    }

    private async void IdleTimer_Tick(object? sender, EventArgs e)
    {
        _idleTimer.Stop();
        try
        {
            await (_ollamaClient?.UnloadModelAsync(App.ConfigManager.Config.OllamaModel)
                   ?? Task.CompletedTask);
        }
        catch (Exception ex)
        {
            App.WriteCrash("MainWindow.IdleTimer_Tick", ex);
        }
    }

    private void ClipboardHintTimer_Tick(object? sender, EventArgs e)
    {
        _clipboardHintTimer.Stop();
        if (!ChatViewModel.IsBusy)
            ChatViewModel.StatusText = "";
    }
}
