using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public interface ITool
{
    string Name { get; }
    string Description { get; }
    ToolParameterSchema Parameters { get; }
    Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args);
}
