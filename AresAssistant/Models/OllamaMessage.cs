using Newtonsoft.Json;

namespace AresAssistant.Models;

public class OllamaMessage
{
    [JsonProperty("role")]
    public string Role { get; set; }

    [JsonProperty("content")]
    public string Content { get; set; }

    [JsonProperty("tool_calls", NullValueHandling = NullValueHandling.Ignore)]
    public List<OllamaToolCall>? ToolCalls { get; set; }

    [JsonProperty("tool_call_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? ToolCallId { get; set; }

    public OllamaMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }
}
