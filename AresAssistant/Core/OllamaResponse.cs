using Newtonsoft.Json;

namespace AresAssistant.Core;

public class OllamaResponse
{
    [JsonProperty("message")]
    public OllamaMessage Message { get; set; } = new("assistant", "");

    [JsonProperty("done")]
    public bool Done { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; }
}

public class OllamaToolCall
{
    [JsonProperty("function")]
    public OllamaToolCallFunction Function { get; set; } = new();
}

public class OllamaToolCallFunction
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("arguments")]
    public Dictionary<string, Newtonsoft.Json.Linq.JToken> Arguments { get; set; } = new();
}
