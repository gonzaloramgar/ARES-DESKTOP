using System.Collections.ObjectModel;
using System.Windows;
using AresAssistant.Core;
using AresAssistant.Config;
using AresAssistant.Tools;

namespace AresAssistant.ViewModels;

public class ChatMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class ChatViewModel : ViewModelBase
{
    private readonly AgentLoop _agentLoop;
    private readonly ConversationHistory _history;
    private readonly AppConfig _config;
    private readonly ToolRegistry? _toolRegistry;

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

    public ChatViewModel(AgentLoop agentLoop, ConversationHistory history, AppConfig config, ToolRegistry? toolRegistry = null)
    {
        _agentLoop = agentLoop;
        _history = history;
        _config = config;
        _toolRegistry = toolRegistry;

        _agentLoop.ResponseReceived += OnResponseReceived;
        _agentLoop.StatusChanged += OnStatusChanged;
        _agentLoop.TokenReceived += OnTokenReceived;

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

        Messages.Add(new ChatMessage { Role = "user", Content = text });

        await _agentLoop.RunAsync(text);

        if (_config.SaveChatHistory)
            _history.SaveToJson("data/chat-history.json");

        IsBusy = false;
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
                // Force UI refresh by replacing the item
                var idx = Messages.IndexOf(_streamingMessage);
                if (idx >= 0)
                {
                    Messages.RemoveAt(idx);
                    Messages.Insert(idx, _streamingMessage);
                }
            }
        });
    }

    private void OnResponseReceived(string response)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_streamingMessage != null)
            {
                // Streaming already added the message — just ensure final text matches
                _streamingMessage.Content = response;
                var idx = Messages.IndexOf(_streamingMessage);
                if (idx >= 0)
                {
                    Messages.RemoveAt(idx);
                    Messages.Insert(idx, _streamingMessage);
                }
                _streamingMessage = null;
            }
            else
            {
                Messages.Add(new ChatMessage { Role = "assistant", Content = response });
            }
            StatusText = "";
        });
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
}
