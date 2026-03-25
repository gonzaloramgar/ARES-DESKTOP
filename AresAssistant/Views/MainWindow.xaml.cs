using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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

    // Inactivity timer — unloads the model from Ollama RAM after idle period
    private DispatcherTimer? _idleTimer;
    private OllamaClient? _ollamaClient;

    public static ChatViewModel ChatViewModel { get; private set; } = null!;
    public static AgentLoop AgentLoop { get; private set; } = null!;
    public static ToolRegistry ToolRegistry { get; private set; } = null!;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        BuildServices();
        PositionWindow();

        Loaded += OnLoaded;
    }

    private void BuildServices()
    {
        var config = App.ConfigManager.Config;

        var ollamaClient = new OllamaClient();
        _ollamaClient = ollamaClient;
        var history = new ConversationHistory();
        var registry = new ToolRegistry();
        var permManager = new PermissionManager();
        var logger = new ActionLogger("data/logs");
        var dispatcher = new ToolDispatcher(registry, permManager, logger);

        // Register built-in tools
        registry.Register(new CloseAppTool());
        registry.Register(new ScreenshotTool());
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

        // Load auto-generated tools from scan
        registry.LoadFromJson("data/tools.json");

        // Hook confirmation dialog
        dispatcher.ConfirmationRequested += ShowConfirmationDialogAsync;

        // Load history if persistence enabled
        if (config.SaveChatHistory && System.IO.File.Exists("data/chat-history.json"))
            history.LoadFromJson("data/chat-history.json");

        var agentLoop = new AgentLoop(ollamaClient, history, registry, dispatcher, config);

        ToolRegistry = registry;
        AgentLoop = agentLoop;
        ChatViewModel = new ChatViewModel(agentLoop, history, config, registry);

        OverlayControl.DataContext = ChatViewModel;
        FullHudControl.DataContext = ChatViewModel;

        // Reset inactivity timer every time the agent produces a response
        AgentLoop.ResponseReceived += _ => ResetIdleTimer();
        ResetIdleTimer();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _hotkeyManager.Initialize(hwnd);

        RegisterHotkeys();
    }

    private void RegisterHotkeys()
    {
        var cfg = App.ConfigManager.Config;

        try
        {
            var (mods1, key1) = GlobalHotkeyManager.ParseHotkey(cfg.ShowHideHotkey);
            _hotkeyManager.Register(mods1, key1, ToggleVisibility);
        }
        catch { /* ignore invalid hotkey string */ }

        try
        {
            var (mods2, key2) = GlobalHotkeyManager.ParseHotkey(cfg.ToggleModeHotkey);
            _hotkeyManager.Register(mods2, key2, ToggleMode);
        }
        catch { /* ignore */ }
    }

    private void ToggleVisibility()
    {
        _isVisible = !_isVisible;
        if (_isVisible)
            Show();
        else
            Hide();
    }

    public void ToggleMode()
    {
        bool toFullHud = OverlayControl.Visibility == Visibility.Visible;

        if (toFullHud)
        {
            AnimateSizeChange(1200, 800);
            OverlayControl.Visibility = Visibility.Collapsed;
            FullHudControl.Visibility = Visibility.Visible;
        }
        else
        {
            AnimateSizeChange(380, 600);
            FullHudControl.Visibility = Visibility.Collapsed;
            OverlayControl.Visibility = Visibility.Visible;
        }

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
        _idleTimer?.Stop();
        // Unload the model so Ollama releases RAM when ARES exits
        _ = _ollamaClient?.UnloadModelAsync(App.ConfigManager.Config.OllamaModel);
        _hotkeyManager.Dispose();
        base.OnClosed(e);
        ((App)Application.Current).CleanupTray();
        Application.Current.Shutdown();
    }

    private void ResetIdleTimer()
    {
        var minutes = App.ConfigManager.Config.ModelKeepAliveMinutes;
        if (minutes <= 0) return;

        _idleTimer?.Stop();
        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(minutes) };
        _idleTimer.Tick += async (_, _) =>
        {
            _idleTimer.Stop();
            await (_ollamaClient?.UnloadModelAsync(App.ConfigManager.Config.OllamaModel)
                   ?? Task.CompletedTask);
        };
        _idleTimer.Start();
    }
}
