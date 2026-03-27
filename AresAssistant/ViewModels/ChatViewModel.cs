using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using AresAssistant.Core;
using AresAssistant.Config;
using AresAssistant.Tools;

namespace AresAssistant.ViewModels;

public class ChatMessage : INotifyPropertyChanged
{
    private string _content = "";

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

    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class ChatViewModel : ViewModelBase
{
    private readonly AgentLoop _agentLoop;
    private readonly ConversationHistory _history;
    private readonly AppConfig _config;
    private readonly ToolRegistry? _toolRegistry;
    private readonly SpeechEngine? _speech;

    private string _inputText = "";
    private bool _isBusy;
    private string _statusText = "";

    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public ObservableCollection<string> ToolNames { get; } = new();
    public string AssistantName => _config.AssistantName;

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

    public ChatViewModel(AgentLoop agentLoop, ConversationHistory history, AppConfig config, ToolRegistry? toolRegistry = null, SpeechEngine? speech = null)
    {
        _agentLoop = agentLoop;
        _history = history;
        _config = config;
        _toolRegistry = toolRegistry;
        _speech = speech;

        _agentLoop.ResponseReceived += OnResponseReceived;
        _agentLoop.StatusChanged += OnStatusChanged;
        _agentLoop.TokenReceived += OnTokenReceived;
        _agentLoop.ToolExecuting += OnToolExecuting;

        _agentLoop.InitSystemPrompt();

        if (_toolRegistry != null)
        {
            foreach (var tool in _toolRegistry.GetAll())
                ToolNames.Add(tool.Name);
        }
    }

    public async Task SendMessageAsync()
    {
        var text = InputText.Trim();
        if (string.IsNullOrEmpty(text) || IsBusy) return;

        InputText = "";
        IsBusy = true;
        _streamingMessage = null;

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
                _streamingMessage = null;
            }
            else
            {
                Messages.Add(new ChatMessage { Role = "assistant", Content = response });
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

    public void ClearHistory()
    {
        _history.Clear();
        Messages.Clear();
        _agentLoop.InitSystemPrompt();
    }

    public void SpeakText(string text) => _speech?.Speak(text);

    public void RemoveMessage(ChatMessage msg) => Messages.Remove(msg);
}
