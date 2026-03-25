using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text;

namespace AresAssistant.Core;

public class OllamaClient
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };
    private const string BaseUrl = "http://localhost:11434";
    private static readonly string DebugLogPath = Path.Combine("data", "logs", "ollama_debug.log");

    public async Task<OllamaResponse> ChatAsync(
        List<OllamaMessage> messages,
        List<ToolDefinition> tools,
        string model)
    {
        var payload = new
        {
            model,
            stream = false,
            messages = messages.Select(m => new
            {
                role = m.Role,
                content = m.Content,
                tool_calls = m.ToolCalls,
                tool_call_id = m.ToolCallId
            }),
            tools,
            options = new { num_ctx = 32768 }
        };

        var json = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        });

#if DEBUG
        WriteDebug("REQUEST", json);
#endif

        var response = await _http.PostAsync(
            $"{BaseUrl}/api/chat",
            new StringContent(json, Encoding.UTF8, "application/json"));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

#if DEBUG
        WriteDebug("RESPONSE", body);
#endif

        return JsonConvert.DeserializeObject<OllamaResponse>(body) ?? new OllamaResponse();
    }

    private static void WriteDebug(string tag, string content)
    {
        try
        {
            Directory.CreateDirectory(Path.Combine("data", "logs"));
            var line = $"[{DateTime.Now:HH:mm:ss}] [{tag}] {content}{Environment.NewLine}";
            File.AppendAllText(DebugLogPath, line);
        }
        catch { /* never crash on logging */ }
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{BaseUrl}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> GetInstalledModelsAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{BaseUrl}/api/tags");
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            var obj = JObject.Parse(body);
            return obj["models"]?
                .Select(m => m["name"]?.ToString() ?? "")
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList() ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
