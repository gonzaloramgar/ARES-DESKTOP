using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using AresAssistant.Core;
using AresAssistant.Config;
using AresAssistant.Tools;
using Newtonsoft.Json.Linq;

namespace AresAssistant.ViewModels;

/// <summary>
/// Representa un mensaje individual en la conversación de chat.
/// Notifica cambios en <see cref="Content"/> para actualizar el streaming en tiempo real.
/// </summary>
public class ChatMessage : INotifyPropertyChanged
{
    private string _content = "";
    private string _modelUsed = "";

    public string Role { get; set; } = "";

    public string Content
    {
        get => _content;
        set
        {
            if (_content != value)
            {
                _content = value;
                OnPropertyChanged();
            }
        }
    }

    public string ModelUsed
    {
        get => _modelUsed;
        set
        {
            if (_modelUsed != value)
            {
                _modelUsed = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class DashboardAppBar
{
    public string AppName { get; set; } = "";
    public string TimeText { get; set; } = "";
    public string ShareText { get; set; } = "";
    public double Percentage { get; set; }
}

public sealed class DashboardTaskCard
{
    public string Label { get; set; } = "";
    public string Time { get; set; } = "";
    public string InText { get; set; } = "";
}

/// <summary>
/// ViewModel del chat principal. Gestiona el envío de mensajes,
/// recepción de tokens en streaming, ejecución de herramientas
/// y síntesis de voz de las respuestas.
/// </summary>
public class ChatViewModel : ViewModelBase
{
    private readonly AgentLoop _agentLoop;
    private readonly ConversationHistory _history;
    private AppConfig _config;
    private readonly ToolRegistry? _toolRegistry;
    private readonly SpeechEngine? _speech;
    private readonly ScheduledTaskStore? _scheduledTaskStore;
    private readonly ProductivityTracker? _productivityTracker;
    private readonly OllamaClient? _ollamaClient;
    private readonly DispatcherTimer _dashboardTimer;
    private readonly HttpClient _dashboardHttp = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly PerformanceCounter? _cpuCounter;

    private string _inputText = "";
    private bool _isBusy;
    private string _statusText = "";
    private string _lastModelUsed = "";
    private string _dashboardClock = DateTime.Now.ToString("HH:mm:ss");
    private string _worldClockNy = "--:--";
    private string _worldClockTokyo = "--:--";
    private string _cpuText = "CPU --%";
    private string _ramText = "RAM --%";
    private string _weatherText = "Clima: cargando...";
    private string _dailyProductivitySummary = "Pulsa 'Resumen IA' para generar el resumen diario.";
    private bool _weatherRefreshInFlight;
    private DateTime _lastWeatherRefreshUtc = DateTime.MinValue;

    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public ObservableCollection<string> ToolNames { get; } = new();
    public ObservableCollection<DashboardAppBar> ProductivityApps { get; } = new();
    public ObservableCollection<DashboardTaskCard> UpcomingTasks { get; } = new();
    public string AssistantName => _config.AssistantName;
    public bool WidgetWeatherEnabled => _config.WidgetWeatherEnabled;
    public bool WidgetWorldClockEnabled => _config.WidgetWorldClockEnabled;
    public bool WidgetSystemLiveEnabled => _config.WidgetSystemLiveEnabled;
    public bool WidgetTasksEnabled => _config.WidgetTasksEnabled;
    public bool WidgetProductivityEnabled => _config.WidgetProductivityEnabled;

    public string InputText
    {
        get => _inputText;
        set => SetField(ref _inputText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public string DashboardClock
    {
        get => _dashboardClock;
        set => SetField(ref _dashboardClock, value);
    }

    public string WorldClockNy
    {
        get => _worldClockNy;
        set => SetField(ref _worldClockNy, value);
    }

    public string WorldClockTokyo
    {
        get => _worldClockTokyo;
        set => SetField(ref _worldClockTokyo, value);
    }

    public string CpuText
    {
        get => _cpuText;
        set => SetField(ref _cpuText, value);
    }

    public string RamText
    {
        get => _ramText;
        set => SetField(ref _ramText, value);
    }

    public string WeatherText
    {
        get => _weatherText;
        set => SetField(ref _weatherText, value);
    }

    public string DailyProductivitySummary
    {
        get => _dailyProductivitySummary;
        set => SetField(ref _dailyProductivitySummary, value);
    }

    public ChatViewModel(
        AgentLoop agentLoop,
        ConversationHistory history,
        AppConfig config,
        ToolRegistry? toolRegistry = null,
        SpeechEngine? speech = null,
        ScheduledTaskStore? scheduledTaskStore = null,
        ProductivityTracker? productivityTracker = null,
        OllamaClient? ollamaClient = null)
    {
        _agentLoop = agentLoop;
        _history = history;
        _config = config;
        _toolRegistry = toolRegistry;
        _speech = speech;
        _scheduledTaskStore = scheduledTaskStore;
        _productivityTracker = productivityTracker;
        _ollamaClient = ollamaClient;

        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _ = _cpuCounter.NextValue();
        }
        catch
        {
            _cpuCounter = null;
        }

        _dashboardTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _dashboardTimer.Tick += async (_, _) => await UpdateDashboardAsync();

        _agentLoop.ResponseReceived += OnResponseReceived;
        _agentLoop.StatusChanged += OnStatusChanged;
        _agentLoop.TokenReceived += OnTokenReceived;
        _agentLoop.ToolExecuting += OnToolExecuting;
        _agentLoop.ModelUsed += OnModelUsed;

        _agentLoop.InitSystemPrompt();

        if (_toolRegistry != null)
        {
            foreach (var tool in _toolRegistry.GetAll())
                ToolNames.Add(tool.Name);
        }

        var savedSummary = _productivityTracker?.GetDailySummary();
        if (!string.IsNullOrWhiteSpace(savedSummary))
            DailyProductivitySummary = savedSummary;

        _dashboardTimer.Start();
        _ = UpdateDashboardAsync();
    }

    public async Task SendMessageAsync()
    {
        var text = InputText.Trim();
        if (string.IsNullOrEmpty(text) || IsBusy) return;

        InputText = "";
        IsBusy = true;
        _streamingMessage = null;
        _lastModelUsed = "";

        // Stop any in-progress speech when the user sends a new message
        _speech?.Stop();

        Messages.Add(new ChatMessage { Role = "user", Content = text });

        try
        {
            await Task.Run(() => _agentLoop.RunAsync(text));
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(new ChatMessage { Role = "assistant", Content = $"Error inesperado: {ex.Message}" });
            });
        }
        finally
        {
            if (_config.SaveChatHistory)
                _history.SaveToJson("data/chat-history.json");

            IsBusy = false;
        }
    }

    private ChatMessage? _streamingMessage;

    private void OnTokenReceived(string token)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_streamingMessage == null)
            {
                _streamingMessage = new ChatMessage { Role = "assistant", Content = token };
                Messages.Add(_streamingMessage);
            }
            else
            {
                _streamingMessage.Content += token;
            }
        });
    }

    private void OnToolExecuting()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_streamingMessage != null)
            {
                Messages.Remove(_streamingMessage);
                _streamingMessage = null;
            }
        });
    }

    private void OnResponseReceived(string response)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_streamingMessage != null)
            {
                _streamingMessage.Content = response;
                _streamingMessage.ModelUsed = _lastModelUsed;
                _streamingMessage = null;
            }
            else
            {
                Messages.Add(new ChatMessage { Role = "assistant", Content = response, ModelUsed = _lastModelUsed });
            }
            StatusText = "";
        });

        // Speak the response aloud (fire-and-forget, runs on SpeechSynthesizer's own thread)
        _speech?.Speak(response);
    }

    private void OnStatusChanged(string status)
    {
        Application.Current.Dispatcher.Invoke(() => StatusText = status);
    }

    private void OnModelUsed(string model)
    {
        _lastModelUsed = model ?? "";
    }

    public void ClearHistory()
    {
        _history.Clear();
        Messages.Clear();
        _agentLoop.InitSystemPrompt();
    }

    public void SpeakText(string text) => _speech?.Speak(text);

    public void RemoveMessage(ChatMessage msg) => Messages.Remove(msg);

    public void RefreshRuntimeConfig(AppConfig config)
    {
        _config = config;
        OnPropertyChanged(nameof(WidgetWeatherEnabled));
        OnPropertyChanged(nameof(WidgetWorldClockEnabled));
        OnPropertyChanged(nameof(WidgetSystemLiveEnabled));
        OnPropertyChanged(nameof(WidgetTasksEnabled));
        OnPropertyChanged(nameof(WidgetProductivityEnabled));

        // Apply new widget settings immediately instead of waiting for next timer tick.
        _ = UpdateDashboardAsync();
    }

    public async Task RefreshProductivitySummaryAsync()
    {
        if (_productivityTracker == null)
        {
            DailyProductivitySummary = "Tracker de productividad no disponible.";
            return;
        }

        var snapshot = _productivityTracker.BuildDailySnapshotText();

        if (_ollamaClient == null)
        {
            DailyProductivitySummary = "No se pudo usar IA local para el resumen (cliente Ollama no disponible).";
            return;
        }

        DailyProductivitySummary = "Generando resumen IA local...";

        var model = _config.MultiModelEnabled
            ? _config.MultiModelReasoningModel
            : _config.OllamaModel;

        var messages = new List<OllamaMessage>
        {
            new("system", "Resume productividad en español con 3 bullets accionables y una conclusión corta. Mantén un tono directo."),
            new("user", "Genera un resumen diario claro basado en estos datos:\n" + snapshot)
        };

        try
        {
            var response = await _ollamaClient.ChatAsync(messages, new List<ToolDefinition>(), model, keepAlive: "5m");
            var text = response.Message?.Content?.Trim();
            if (!string.IsNullOrWhiteSpace(response.Error) || string.IsNullOrWhiteSpace(text))
            {
                DailyProductivitySummary = "No se pudo generar el resumen IA. Revisa que el modelo local esté disponible.";
                return;
            }

            DailyProductivitySummary = text;
            _productivityTracker.SetDailySummary(text);
        }
        catch (Exception ex)
        {
            DailyProductivitySummary = $"Error generando resumen IA: {ex.Message}";
        }
    }

    private async Task UpdateDashboardAsync()
    {
        DashboardClock = DateTime.Now.ToString("HH:mm:ss");

        if (WidgetWorldClockEnabled)
        {
            WorldClockNy = BuildClock("Eastern Standard Time", "NY", TimeSpan.FromHours(-4));
            WorldClockTokyo = BuildClock("Tokyo Standard Time", "TOK", TimeSpan.FromHours(9));
        }
        else
        {
            WorldClockNy = "NY --:--";
            WorldClockTokyo = "TOK --:--";
        }

        if (WidgetSystemLiveEnabled)
            UpdateSystemStats();
        else
        {
            CpuText = "CPU desactivado";
            RamText = "RAM desactivada";
        }

        if (WidgetTasksEnabled)
            UpdateUpcomingTasks();
        else
            UpcomingTasks.Clear();

        if (WidgetProductivityEnabled)
            UpdateProductivityBars();
        else
            ProductivityApps.Clear();

        if (!WidgetWeatherEnabled)
        {
            WeatherText = "Clima desactivado";
            return;
        }

        if (!_weatherRefreshInFlight &&
            (DateTime.UtcNow - _lastWeatherRefreshUtc) > TimeSpan.FromMinutes(20))
        {
            await RefreshWeatherAsync();
        }
    }

    private void UpdateSystemStats()
    {
        try
        {
            var cpu = _cpuCounter != null ? Math.Clamp(_cpuCounter.NextValue(), 0, 100) : 0;
            CpuText = _cpuCounter != null ? $"CPU {cpu:0}%" : "CPU n/d";
        }
        catch
        {
            CpuText = "CPU n/d";
        }

        try
        {
            var ram = GetUsedRamPercent();
            RamText = $"RAM {ram:0}%";
        }
        catch
        {
            RamText = "RAM n/d";
        }
    }

    private void UpdateUpcomingTasks()
    {
        if (_scheduledTaskStore == null) return;

        var now = TimeOnly.FromDateTime(DateTime.Now);
        var upcoming = _scheduledTaskStore
            .GetAll()
            .Where(t => t.Enabled)
            .Select(t =>
            {
                var parsed = TimeOnly.TryParseExact(t.Time, "HH:mm", out var taskTime)
                    ? taskTime
                    : now;
                var delta = taskTime.ToTimeSpan() - now.ToTimeSpan();
                if (delta.TotalMinutes < 0) delta += TimeSpan.FromDays(1);
                return new
                {
                    task = t,
                    minutes = (int)Math.Round(delta.TotalMinutes)
                };
            })
            .OrderBy(x => x.minutes)
            .Take(5)
            .ToList();

        UpcomingTasks.Clear();
        foreach (var item in upcoming)
        {
            var label = string.IsNullOrWhiteSpace(item.task.Description)
                ? item.task.Command
                : item.task.Description;
            if (label.Length > 38) label = label[..38] + "...";

            UpcomingTasks.Add(new DashboardTaskCard
            {
                Label = label,
                Time = item.task.Time,
                InText = item.minutes <= 0 ? "ahora" : $"en {item.minutes} min"
            });
        }
    }

    private void UpdateProductivityBars()
    {
        if (_productivityTracker == null) return;

        var data = _productivityTracker.GetTopApps(6);
        var total = Math.Max(1, data.Sum(d => d.Seconds));
        var max = Math.Max(1, data.Max(d => d.Seconds));

        ProductivityApps.Clear();
        foreach (var item in data)
        {
            var minutes = Math.Round(item.Seconds / 60.0, 1);
            var share = (int)Math.Round(item.Seconds * 100.0 / total);
            ProductivityApps.Add(new DashboardAppBar
            {
                AppName = item.AppName,
                TimeText = $"{minutes} min",
                ShareText = $"{share}%",
                Percentage = item.Seconds * 100.0 / max
            });
        }
    }

    private async Task RefreshWeatherAsync()
    {
        _weatherRefreshInFlight = true;
        try
        {
            var location = await LocationResolver.ResolveByIpAsync();
            if (location == null)
            {
                WeatherText = "Clima no disponible";
                return;
            }

            var city = string.IsNullOrWhiteSpace(location.City) ? "Local" : location.City;
            var lat = location.Latitude;
            var lon = location.Longitude;

            if (lat == 0 && lon == 0)
            {
                WeatherText = "Clima no disponible";
                return;
            }

            var latText = lat.ToString(CultureInfo.InvariantCulture);
            var lonText = lon.ToString(CultureInfo.InvariantCulture);
            var wxJson = await _dashboardHttp.GetStringAsync(
                $"https://api.open-meteo.com/v1/forecast?latitude={latText}&longitude={lonText}&current=temperature_2m,weather_code&timezone=auto");
            var wx = JObject.Parse(wxJson);
            var temp = wx["current"]?["temperature_2m"]?.ToObject<double>();
            var code = wx["current"]?["weather_code"]?.ToObject<int>() ?? -1;
            WeatherText = temp.HasValue
                ? $"{city}: {temp.Value:0.#}°C · {DecodeWeatherCode(code)}"
                : $"{city}: clima no disponible";
            _lastWeatherRefreshUtc = DateTime.UtcNow;
        }
        catch
        {
            WeatherText = "Clima no disponible";
        }
        finally
        {
            _weatherRefreshInFlight = false;
        }
    }

    private static string BuildClock(string timeZoneId, string label, TimeSpan fallbackOffset)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var value = TimeZoneInfo.ConvertTime(DateTime.Now, tz);
            return $"{label} {value:HH:mm}";
        }
        catch
        {
            var value = DateTime.UtcNow + fallbackOffset;
            return $"{label} {value:HH:mm}";
        }
    }

    private static string DecodeWeatherCode(int code) => code switch
    {
        0 => "despejado",
        1 => "claro",
        2 => "parcialmente nublado",
        3 => "nublado",
        45 or 48 => "niebla",
        51 or 53 or 55 => "llovizna",
        61 or 63 or 65 => "lluvia",
        71 or 73 or 75 => "nieve",
        80 or 81 or 82 => "chubascos",
        95 => "tormenta",
        _ => "variable"
    };

    private static double GetUsedRamPercent()
    {
        var mem = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(mem)) return 0;
        var total = mem.ullTotalPhys;
        var avail = mem.ullAvailPhys;
        if (total <= 0) return 0;
        return Math.Clamp((total - avail) * 100.0 / total, 0, 100);
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] MemoryStatusEx lpBuffer);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MemoryStatusEx()
        {
            dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MemoryStatusEx));
        }
    }
}
