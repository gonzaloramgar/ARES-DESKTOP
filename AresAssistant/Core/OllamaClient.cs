using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text;

namespace AresAssistant.Core;

public class OllamaClient
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(2) };
    private const string BaseUrl = "http://localhost:11434";
    private static readonly string DebugLogPath = Path.Combine("data", "logs", "ollama_debug.log");

    /// <summary>
    /// Standard request (no streaming). Used for tool-call iterations
    /// where we need the complete response before acting.
    /// </summary>
    public async Task<OllamaResponse> ChatAsync(
        List<OllamaMessage> messages,
        List<ToolDefinition> tools,
        string model,
        int numCtx = 4096,
        int numThread = 0,
        int numPredict = 512,
        int numBatch = 512)
    {
        var options = numThread > 0
            ? (object)new { num_ctx = numCtx, num_thread = numThread, num_predict = numPredict, num_batch = numBatch, temperature = 0.7, repeat_penalty = 1.1 }
            : new { num_ctx = numCtx, num_predict = numPredict, num_batch = numBatch, temperature = 0.7, repeat_penalty = 1.1 };

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
            options
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
            new StringContent(json, Encoding.UTF8, "application/json")).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

#if DEBUG
        WriteDebug("RESPONSE", body);
#endif

        return JsonConvert.DeserializeObject<OllamaResponse>(body) ?? new OllamaResponse();
    }

    /// <summary>
    /// Streaming request. Yields text tokens incrementally for real-time display.
    /// Falls back to non-streaming if the first response contains tool_calls.
    /// </summary>
    public async IAsyncEnumerable<StreamChunk> ChatStreamAsync(
        List<OllamaMessage> messages,
        List<ToolDefinition> tools,
        string model,
        int numCtx = 4096,
        int numThread = 0,
        int numPredict = 512,
        int numBatch = 512)
    {
        var options = numThread > 0
            ? (object)new { num_ctx = numCtx, num_thread = numThread, num_predict = numPredict, num_batch = numBatch, temperature = 0.7, repeat_penalty = 1.1 }
            : new { num_ctx = numCtx, num_predict = numPredict, num_batch = numBatch, temperature = 0.7, repeat_penalty = 1.1 };

        var payload = new
        {
            model,
            stream = true,
            messages = messages.Select(m => new
            {
                role = m.Role,
                content = m.Content,
                tool_calls = m.ToolCalls,
                tool_call_id = m.ToolCallId
            }),
            tools,
            options
        };

        var json = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        });

        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/chat")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line)) continue;

            JObject chunk;
            try { chunk = JObject.Parse(line); }
            catch { continue; }

            var msg = chunk["message"];
            if (msg == null) continue;

            // If tool_calls present in any chunk → yield the full parsed response
            var toolCalls = msg["tool_calls"];
            if (toolCalls != null && toolCalls.HasValues)
            {
                var fullResp = JsonConvert.DeserializeObject<OllamaResponse>(line);
                yield return new StreamChunk { ToolResponse = fullResp };
                yield break;
            }

            var token = msg["content"]?.ToString();
            if (!string.IsNullOrEmpty(token))
                yield return new StreamChunk { Token = token };

            if (chunk["done"]?.Value<bool>() == true) yield break;
        }
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

    /// <summary>
    /// Tells Ollama to unload <paramref name="model"/> from RAM immediately.
    /// Uses keep_alive=0 per the Ollama API spec.
    /// </summary>
    public async Task UnloadModelAsync(string model)
    {
        try
        {
            var payload = JsonConvert.SerializeObject(new { model, keep_alive = 0 });
            await _http.PostAsync(
                $"{BaseUrl}/api/generate",
                new StringContent(payload, Encoding.UTF8, "application/json"));
        }
        catch { /* best-effort — Ollama may not be running */ }
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

    /// <summary>
    /// Pulls (downloads) a model from the Ollama library.
    /// Reports progress via <paramref name="onProgress"/> (0.0–1.0) and status text.
    /// </summary>
    public async Task PullModelAsync(string model, Action<double, string>? onProgress = null, CancellationToken ct = default)
    {
        using var pullHttp = new HttpClient { Timeout = TimeSpan.FromHours(2) };
        var payload = JsonConvert.SerializeObject(new { name = model, stream = true });
        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/pull")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var response = await pullHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var obj = JObject.Parse(line);
                var status = obj["status"]?.ToString() ?? "";
                var total = obj["total"]?.Value<long>() ?? 0;
                var completed = obj["completed"]?.Value<long>() ?? 0;
                var pct = total > 0 ? (double)completed / total : 0;
                onProgress?.Invoke(pct, status);
            }
            catch { /* skip malformed lines */ }
        }
    }
}

/// <summary>One chunk from the streaming response — either a text token or a full tool-call response.</summary>
public class StreamChunk
{
    public string? Token { get; init; }
    public OllamaResponse? ToolResponse { get; init; }
    public bool IsToolCall => ToolResponse != null;
}
