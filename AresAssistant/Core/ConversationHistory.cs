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

    public void SaveToJson(string path)
    {
        var json = JsonConvert.SerializeObject(_messages, Formatting.Indented);
        File.WriteAllText(path, json);
    }
}
