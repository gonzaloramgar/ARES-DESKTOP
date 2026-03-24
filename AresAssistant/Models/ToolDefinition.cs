using Newtonsoft.Json;

namespace AresAssistant.Models;

public class ToolDefinition
{
    [JsonProperty("type")]
    public string Type { get; set; } = "function";

    [JsonProperty("function")]
    public ToolFunction Function { get; set; } = new();
}

public class ToolFunction
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("description")]
    public string Description { get; set; } = "";

    [JsonProperty("parameters")]
    public ToolParameterSchema Parameters { get; set; } = new();
}

public class ToolParameterSchema
{
    [JsonProperty("type")]
    public string Type { get; set; } = "object";

    [JsonProperty("properties")]
    public Dictionary<string, ToolParameterProperty> Properties { get; set; } = new();

    [JsonProperty("required")]
    public List<string> Required { get; set; } = new();
}

public class ToolParameterProperty
{
    [JsonProperty("type")]
    public string Type { get; set; } = "string";

    [JsonProperty("description")]
    public string Description { get; set; } = "";

    [JsonProperty("default", NullValueHandling = NullValueHandling.Ignore)]
    public object? Default { get; set; }
}
