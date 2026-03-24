using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text;

namespace AresAssistant.Core;

public class OllamaClient
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };
    private const string BaseUrl = "http://localhost:11434";

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
                tool_calls = m.ToolCalls
            }),
            tools
        };

        var json = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });

        var response = await _http.PostAsync(
            $"{BaseUrl}/api/chat",
            new StringContent(json, Encoding.UTF8, "application/json"));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<OllamaResponse>(body) ?? new OllamaResponse();
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
