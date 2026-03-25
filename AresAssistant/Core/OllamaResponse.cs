using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("function")]
    public OllamaToolCallFunction Function { get; set; } = new();
}

public class OllamaToolCallFunction
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("arguments")]
    [JsonConverter(typeof(ToolArgumentsConverter))]
    public Dictionary<string, JToken> Arguments { get; set; } = new();
}

/// <summary>
/// Ollama sometimes returns arguments as a JSON string instead of a JSON object.
/// This converter handles both cases.
/// </summary>
public class ToolArgumentsConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
        => objectType == typeof(Dictionary<string, JToken>);

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var token = JToken.Load(reader);

        if (token.Type == JTokenType.Null)
            return new Dictionary<string, JToken>();

        // Arguments returned as a JSON string — parse it
        if (token.Type == JTokenType.String)
        {
            var str = token.Value<string>() ?? "{}";
            try
            {
                var parsed = JObject.Parse(str);
                return parsed.ToObject<Dictionary<string, JToken>>() ?? new();
            }
            catch
            {
                return new Dictionary<string, JToken>();
            }
        }

        // Arguments returned as a JSON object (normal path)
        if (token.Type == JTokenType.Object)
            return ((JObject)token).ToObject<Dictionary<string, JToken>>() ?? new();

        return new Dictionary<string, JToken>();
    }

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        => serializer.Serialize(writer, value);
}
