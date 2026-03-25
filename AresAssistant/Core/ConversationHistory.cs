using Newtonsoft.Json;

namespace AresAssistant.Core;

public class ConversationHistory
{
    private readonly List<OllamaMessage> _messages = new();

    public void Add(OllamaMessage message) => _messages.Add(message);

    public void Clear() => _messages.Clear();

    public List<OllamaMessage> ToList() => new(_messages);

    public int Count => _messages.Count;

    public void ExportToTxt(string path)
    {
        var lines = _messages.Select(m =>
            $"[{m.Role.ToUpper()}]: {m.Content}");
        File.WriteAllLines(path, lines);
    }

    public void LoadFromJson(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            var json = File.ReadAllText(path);
            var msgs = JsonConvert.DeserializeObject<List<OllamaMessage>>(json);
            if (msgs != null)
            {
                _messages.Clear();
                _messages.AddRange(msgs);
            }
        }
        catch { /* ignore corrupt history */ }
    }

    public void ReplaceSystemPrompt(string content)
    {
        if (_messages.Count > 0 && _messages[0].Role == "system")
            _messages[0] = new OllamaMessage("system", content);
        else
            _messages.Insert(0, new OllamaMessage("system", content));
    }

    public void SaveToJson(string path)
    {
        var json = JsonConvert.SerializeObject(_messages, Formatting.Indented);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Keeps only the system message plus the most recent <paramref name="maxMessages"/> messages.
    /// Prevents the context window from growing unboundedly over long conversations.
    /// </summary>
    public void TrimToLast(int maxMessages)
    {
        OllamaMessage? systemMsg = _messages.Count > 0 && _messages[0].Role == "system"
            ? _messages[0]
            : null;

        var nonSystem = _messages.Skip(systemMsg != null ? 1 : 0).ToList();
        if (nonSystem.Count <= maxMessages) return;

        _messages.Clear();
        if (systemMsg != null) _messages.Add(systemMsg);
        _messages.AddRange(nonSystem.TakeLast(maxMessages));
    }
}
