using Newtonsoft.Json;

namespace AresAssistant.Core;

public class ConversationHistory
{
    private readonly List<OllamaMessage> _messages = new();
    private readonly object _lock = new();

    public void Add(OllamaMessage message) { lock (_lock) _messages.Add(message); }

    public void Clear() { lock (_lock) _messages.Clear(); }

    public List<OllamaMessage> ToList() { lock (_lock) return new(_messages); }

    public int Count { get { lock (_lock) return _messages.Count; } }

    public void ExportToTxt(string path)
    {
        List<OllamaMessage> snapshot;
        lock (_lock) snapshot = new(_messages);
        var lines = snapshot.Select(m =>
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
                lock (_lock)
                {
                    _messages.Clear();
                    _messages.AddRange(msgs);
                }
            }
        }
        catch { /* ignore corrupt history */ }
    }

    public void ReplaceSystemPrompt(string content)
    {
        lock (_lock)
        {
            if (_messages.Count > 0 && _messages[0].Role == "system")
                _messages[0] = new OllamaMessage("system", content);
            else
                _messages.Insert(0, new OllamaMessage("system", content));
        }
    }

    /// <summary>
    /// Removes tool responses that reported failures ("no encontrada", "Error") AND
    /// their parent assistant tool_call + any assistant follow-up that echoed the error.
    /// This prevents stale "app not found" results from poisoning future model behaviour.
    /// </summary>
    public void PurgeToolFailures()
    {
        lock (_lock)
        {
            var toRemove = new HashSet<int>();

            for (int i = 0; i < _messages.Count; i++)
            {
                if (_messages[i].Role != "tool") continue;
                var content = _messages[i].Content ?? "";
                if (!content.Contains("no encontrada", StringComparison.OrdinalIgnoreCase)
                    && !content.Contains("Error", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Mark this tool message for removal
                toRemove.Add(i);

                // Walk backwards to find the parent assistant with tool_calls
                for (int j = i - 1; j >= 0; j--)
                {
                    if (_messages[j].Role == "tool") { toRemove.Add(j); continue; }
                    if (_messages[j].Role == "assistant" && _messages[j].ToolCalls?.Count > 0)
                    { toRemove.Add(j); break; }
                    break;
                }

                // Walk forward to remove the assistant echo that reported the failure
                if (i + 1 < _messages.Count && _messages[i + 1].Role == "assistant"
                    && (_messages[i + 1].ToolCalls == null || _messages[i + 1].ToolCalls!.Count == 0))
                    toRemove.Add(i + 1);
            }

            if (toRemove.Count == 0) return;

            for (int i = _messages.Count - 1; i >= 0; i--)
            {
                if (toRemove.Contains(i))
                    _messages.RemoveAt(i);
            }
        }
    }

    public void SaveToJson(string path)
    {
        List<OllamaMessage> snapshot;
        lock (_lock) snapshot = new(_messages);
        var json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Keeps only the system message plus the most recent <paramref name="maxMessages"/> messages.
    /// Never splits an assistant→tool group so the model always sees complete tool-call sequences.
    /// </summary>
    public void TrimToLast(int maxMessages)
    {
        lock (_lock)
        {
            OllamaMessage? systemMsg = _messages.Count > 0 && _messages[0].Role == "system"
                ? _messages[0]
                : null;

            var nonSystem = _messages.Skip(systemMsg != null ? 1 : 0).ToList();
            if (nonSystem.Count <= maxMessages) return;

            // Take last N, then walk backwards to include the full tool-call group
            int start = nonSystem.Count - maxMessages;

            // If we'd start on a "tool" message, walk back to include
            // the preceding tool messages and their parent assistant message
            while (start > 0 && nonSystem[start].Role == "tool")
                start--;
            // Also include the assistant that triggered the tool calls
            if (start > 0 && nonSystem[start - 1].Role == "assistant"
                && nonSystem[start - 1].ToolCalls?.Count > 0)
                start--;

            _messages.Clear();
            if (systemMsg != null) _messages.Add(systemMsg);
            _messages.AddRange(nonSystem.Skip(start));
        }
    }
}
