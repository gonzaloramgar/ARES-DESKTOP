using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using AresAssistant.Config;
using AresAssistant.Tools;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Core;

public class AgentLoop
{
    private const int MaxIterations = 10;

    private readonly OllamaClient _ollamaClient;
    private readonly ConversationHistory _history;
    private readonly ToolRegistry _toolRegistry;
    private readonly ToolDispatcher _toolDispatcher;
    private readonly AppConfig _config;

    /// <summary>Fired when the complete response is ready (after streaming ends).</summary>
    public event Action<string>? ResponseReceived;
    /// <summary>Fired for each streaming token so the UI can display text word by word.</summary>
    public event Action<string>? TokenReceived;
    public event Action<string>? StatusChanged;
    /// <summary>Fired when tool calls are about to execute so the UI can remove any partial streaming bubble.</summary>
    public event Action? ToolExecuting;

    private string? _cachedSystemPrompt;
    private string? _lastPersonality;
    private string? _lastResponseLength;
    private string? _lastAssistantName;
    private string? _lastPerformanceMode;

    public AgentLoop(
        OllamaClient ollamaClient,
        ConversationHistory history,
        ToolRegistry toolRegistry,
        ToolDispatcher toolDispatcher,
        AppConfig config)
    {
        _ollamaClient = ollamaClient;
        _history = history;
        _toolRegistry = toolRegistry;
        _toolDispatcher = toolDispatcher;
        _config = config;
    }

    public void InitSystemPrompt()
    {
        var systemPrompt = BuildSystemPrompt();

        if (_history.Count == 0)
            _history.Add(new OllamaMessage("system", systemPrompt));
        else
            _history.ReplaceSystemPrompt(systemPrompt);
    }

    /// <summary>
    /// Pre-loads the model into RAM using a zero-inference request (empty prompt).
    /// Returns as soon as the model is loaded — no tokens generated, no blocking.
    /// Fire-and-forget — failures are silently ignored.
    /// </summary>
    public async Task WarmUpAsync()
    {
        var keepAlive = _config.ModelKeepAliveMinutes > 0 ? $"{_config.ModelKeepAliveMinutes}m" : "-1";
        await _ollamaClient.PreloadModelAsync(_config.OllamaModel, keepAlive);
    }

    private string BuildSystemPrompt()
    {
        // Cache: only rebuild when personality/length/name/mode changes
        if (_cachedSystemPrompt != null
            && _lastPersonality == _config.Personality
            && _lastResponseLength == _config.ResponseLength
            && _lastAssistantName == _config.AssistantName
            && _lastPerformanceMode == _config.PerformanceMode)
            return _cachedSystemPrompt;

        _lastPersonality = _config.Personality;
        _lastResponseLength = _config.ResponseLength;
        _lastAssistantName = _config.AssistantName;
        _lastPerformanceMode = _config.PerformanceMode;

        var desktop   = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var userName  = Environment.UserName;

        var sb = new StringBuilder();
        sb.AppendLine($"Eres {_config.AssistantName}, un asistente de IA integrado en el sistema operativo del usuario.");
        sb.AppendLine("SIEMPRE respondes en español de España. NUNCA cambies de idioma bajo ninguna circunstancia.");
        sb.AppendLine("No escribas en chino, inglés, ni ningún otro idioma. Solo español.");
        sb.AppendLine("Eres directo, eficiente y ligeramente formal.");
        sb.AppendLine();

        if (_config.PerformanceMode == "avanzado")
        {
            // qwen2.5:14b — modelo capaz, prompt compacto (~200 tokens menos de prefill)
            sb.AppendLine("## HERRAMIENTAS");
            sb.AppendLine("Llama siempre a la herramienta ANTES de responder. Nunca describas una acción sin ejecutarla primero.");
            sb.AppendLine("Disponibles: open_app, open_folder, close_app, create_folder, delete_folder, recycle_bin,");
            sb.AppendLine("  read_file, write_file, search_web, search_browser, run_command, screenshot, type_text,");
            sb.AppendLine("  system_info, volume, list_open_windows, minimize_window, maximize_window,");
            sb.AppendLine("  clipboard_read, clipboard_write, remember_app, get_location, get_weather.");
            sb.AppendLine("Clima: get_location primero, luego get_weather. App desconocida con ruta: remember_app.");
            sb.AppendLine("Resultado de herramienta → informa brevemente, no repitas la misma herramienta.");
            sb.AppendLine();
            sb.AppendLine("## CONTEXTO");
            sb.AppendLine("Referencias a acciones previas ('bórrala', 'deshaz eso') → consulta historial, ejecuta la herramienta adecuada.");
            sb.AppendLine("No borres sistema ni mates procesos críticos. Acción rechazada → no reintentas.");
            sb.AppendLine();
        }
        else
        {
            // qwen2.5:7b — modelo ligero, necesita instrucciones explícitas
            sb.AppendLine("## USO DE HERRAMIENTAS — REGLA ABSOLUTA");
            sb.AppendLine("Tienes herramientas para controlar el ordenador. DEBES USARLAS siempre que el usuario pida una acción.");
            sb.AppendLine("NUNCA describas una acción sin ejecutarla. NUNCA digas 'voy a...', 'procedo a...' sin haber llamado a la herramienta.");
            sb.AppendLine("Si el usuario pide:");
            sb.AppendLine("  - abrir algo             → open_app o open_folder");
            sb.AppendLine("  - cerrar algo             → close_app");
            sb.AppendLine("  - crear carpeta           → create_folder");
            sb.AppendLine("  - eliminar algo           → delete_folder");
            sb.AppendLine("  - papelera                → recycle_bin");
            sb.AppendLine("  - leer/escribir archivo   → read_file / write_file");
            sb.AppendLine("  - buscar en internet      → search_web o search_browser");
            sb.AppendLine("  - ejecutar comando        → run_command");
            sb.AppendLine("  - captura de pantalla     → screenshot");
            sb.AppendLine("  - escribir texto          → type_text");
            sb.AppendLine("  - info del sistema        → system_info");
            sb.AppendLine("  - volumen                 → volume");
            sb.AppendLine("  - ventanas                → list_open_windows, minimize_window, maximize_window");
            sb.AppendLine("  - portapapeles            → clipboard_read, clipboard_write");
            sb.AppendLine("  - recordar app            → remember_app");
            sb.AppendLine("  - ubicación/clima         → get_location PRIMERO, luego get_weather");
            sb.AppendLine("Usa la herramienta PRIMERO. Explica brevemente lo que hiciste DESPUÉS.");
            sb.AppendLine("Cuando la herramienta devuelve un resultado, informa al usuario con una frase breve. NO repitas la misma herramienta.");
            sb.AppendLine("Si open_app no encuentra una app y el usuario da la ruta, usa remember_app para guardarla.");
            sb.AppendLine();
            sb.AppendLine("## CONTEXTO CONVERSACIONAL");
            sb.AppendLine("Recuerda las acciones que acabas de realizar. Si el usuario dice 'bórrala', 'deshaz eso' → mira mensajes anteriores, identifica el objetivo y ejecuta la herramienta.");
            sb.AppendLine("Nunca borres archivos del sistema ni mates procesos críticos. Si una acción es rechazada, no la reintentas.");
            sb.AppendLine();
        }

        sb.AppendLine("## RUTAS DEL SISTEMA");
        sb.AppendLine($"  Usuario: {userName}");
        sb.AppendLine($"  Escritorio (Desktop): {desktop}");
        sb.AppendLine($"  Documentos: {documents}");
        sb.AppendLine($"  Descargas: {downloads}");
        sb.AppendLine("Alias válidos para rutas: Desktop, Documents, Downloads, Pictures, Music, Videos.");
        sb.AppendLine("Ejemplo para crear carpeta en el escritorio: path='Desktop/NombreCarpeta'");
        sb.AppendLine();

        sb.AppendLine(_config.Personality switch
        {
            "casual"     => "## TONO\nUsa un tono informal y cercano.",
            "sarcastico" => "## TONO\nPuedes ser levemente sarcástico y con humor.",
            "tecnico"    => "## TONO\nUsa terminología técnica precisa.",
            _            => ""
        });

        sb.AppendLine(_config.ResponseLength switch
        {
            "conciso"   => "## LONGITUD\nResponde siempre muy brevemente, máximo 2 frases tras ejecutar la acción.",
            "detallado" => "## LONGITUD\nExplica tus acciones y razonamientos con detalle.",
            _           => ""
        });

        _cachedSystemPrompt = sb.ToString().Trim();
        return _cachedSystemPrompt;
    }

    public async Task RunAsync(string userMessage)
    {
        var (numCtx, numThread, historyLimit, numPredict, numBatch) = _config.GetPerformanceParams();
        var keepAlive = _config.ModelKeepAliveMinutes > 0 ? $"{_config.ModelKeepAliveMinutes}m" : "-1";

        _history.Add(new OllamaMessage("user", userMessage));
        _history.TrimToLast(historyLimit);

        int iterations = 0;

        while (iterations < MaxIterations)
        {
            iterations++;

            // If this is a tool-call loop iteration, use non-streaming to get tool_calls
            // For the potential final text response, try streaming first
            bool useStreaming = iterations == 1 || !HasPendingToolResults();

            if (useStreaming)
            {
                // Attempt streaming — gives instant token-by-token feedback
                var streamed = await TryStreamResponseAsync(numCtx, numThread, numPredict, numBatch, keepAlive).ConfigureAwait(false);
                if (streamed.HasValue)
                {
                    if (streamed.Value.toolResponse != null)
                    {
                        // Got tool calls from stream — process them
                        var resp = streamed.Value.toolResponse;
                        ToolExecuting?.Invoke();
                        _history.Add(new OllamaMessage("assistant", resp.Message.Content ?? "")
                        {
                            ToolCalls = resp.Message.ToolCalls
                        });
                        await ExecuteToolCallsAsync(resp.Message.ToolCalls!).ConfigureAwait(false);
                        continue; // loop back
                    }
                    else
                    {
                        // Got streamed text — check for text-based tool calls first
                        var content = streamed.Value.text ?? "";
                        var (textCalls, cleanedText) = TryParseTextToolCalls(content);

                        if (textCalls.Count > 0)
                        {
                            ToolExecuting?.Invoke();
                            _history.Add(new OllamaMessage("assistant", cleanedText)
                            {
                                ToolCalls = textCalls
                            });
                            await ExecuteToolCallsAsync(textCalls).ConfigureAwait(false);
                            continue; // loop back for the tool result response
                        }

                        _history.Add(new OllamaMessage("assistant", content));
                        StatusChanged?.Invoke("");
                        ResponseReceived?.Invoke(content);
                        return;
                    }
                }
            }

            // Fallback to non-streaming (for tool iterations or if streaming failed)
            OllamaResponse response;
            try
            {
                StatusChanged?.Invoke("Pensando...");
                response = await _ollamaClient.ChatAsync(
                    _history.ToList(),
                    _toolRegistry.GetToolDefinitions(),
                    _config.OllamaModel,
                    numCtx,
                    numThread,
                    numPredict,
                    numBatch,
                    keepAlive).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ResponseReceived?.Invoke($"Error al conectar con Ollama: {ex.Message}");
                return;
            }

            if (!string.IsNullOrEmpty(response.Error))
            {
                ResponseReceived?.Invoke($"Error de Ollama: {response.Error}");
                return;
            }

            if (response.Message.ToolCalls?.Count > 0)
            {
                ToolExecuting?.Invoke();
                _history.Add(new OllamaMessage("assistant", response.Message.Content ?? "")
                {
                    ToolCalls = response.Message.ToolCalls
                });
                await ExecuteToolCallsAsync(response.Message.ToolCalls).ConfigureAwait(false);
            }
            else
            {
                var content = response.Message.Content ?? "";

                // Fallback: detect tool calls embedded as text
                var (textCalls, cleanedText) = TryParseTextToolCalls(content);
                if (textCalls.Count > 0)
                {
                    ToolExecuting?.Invoke();
                    _history.Add(new OllamaMessage("assistant", cleanedText)
                    {
                        ToolCalls = textCalls
                    });
                    await ExecuteToolCallsAsync(textCalls).ConfigureAwait(false);
                    continue; // loop back
                }

                _history.Add(new OllamaMessage("assistant", content));
                StatusChanged?.Invoke("");
                ResponseReceived?.Invoke(content);
                return;
            }
        }

        var fallback = "ARES: He alcanzado el límite de acciones consecutivas. Por favor, reformula tu petición.";
        ResponseReceived?.Invoke(fallback);
    }

    private bool HasPendingToolResults()
    {
        var msgs = _history.ToList();
        return msgs.Count > 0 && msgs[^1].Role == "tool";
    }

    // ═══════════════ Text-based tool call fallback ═══════════════
    // Some models (especially qwen2.5:14b) sometimes emit tool calls as raw text
    // instead of the structured Ollama tool_calls format.  Patterns seen:
    //   <tool_call>\n{"name":"open_app","arguments":{...}}\n</tool_call>
    //   _icall_\n{"name":"open_app","arguments":{...}}\n</tool_call>  (garbled prefix)

    private static readonly Regex TextToolCallRegex = new(
        @"\{[^{}]*""name""\s*:\s*""([^""]+)""\s*,\s*""arguments""\s*:\s*(\{[^{}]*\})",
        RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// Try to extract tool calls embedded as text in the response content.
    /// Returns the list of parsed calls and the cleaned text with tool markup removed.
    /// </summary>
    private static (List<OllamaToolCall> calls, string cleanedText) TryParseTextToolCalls(string content)
    {
        var calls = new List<OllamaToolCall>();
        if (string.IsNullOrWhiteSpace(content)) return (calls, content);

        foreach (Match m in TextToolCallRegex.Matches(content))
        {
            try
            {
                var name = m.Groups[1].Value;
                var argsJson = m.Groups[2].Value;
                var argsObj = JObject.Parse(argsJson);
                var args = argsObj.ToObject<Dictionary<string, JToken>>() ?? new();

                calls.Add(new OllamaToolCall
                {
                    Function = new OllamaToolCallFunction { Name = name, Arguments = args }
                });
            }
            catch { /* malformed JSON — skip */ }
        }

        if (calls.Count > 0)
        {
            // Strip the tool_call markup and any surrounding tags from the visible text
            var cleaned = Regex.Replace(content, @"<?\/?tool_call>?", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"_icall_", "", RegexOptions.IgnoreCase);
            cleaned = TextToolCallRegex.Replace(cleaned, "");
            cleaned = cleaned.Trim('\n', '\r', ' ');
            return (calls, cleaned);
        }

        return (calls, content);
    }

    /// <summary>
    /// Process a list of tool calls (either from structured API or text fallback).
    /// </summary>
    private async Task ExecuteToolCallsAsync(List<OllamaToolCall> toolCalls)
    {
        foreach (var call in toolCalls)
        {
            StatusChanged?.Invoke($"Ejecutando: {call.Function.Name}...");
            var result = await _toolDispatcher.ExecuteAsync(call.Function.Name, call.Function.Arguments)
                .ConfigureAwait(false);
            _history.Add(new OllamaMessage("tool", result) { ToolCallId = call.Id });
        }
    }

    /// <summary>
    /// Tries to get a streaming response. Returns text + optional tool response.
    /// Returns null if streaming fails (caller should fall back to non-streaming).
    /// Buffers initial tokens to detect text-based tool calls before emitting to the UI.
    /// </summary>
    private async Task<(string? text, OllamaResponse? toolResponse)?> TryStreamResponseAsync(int numCtx, int numThread, int numPredict, int numBatch, string keepAlive)
    {
        try
        {
            StatusChanged?.Invoke("Pensando...");
            var fullText = new StringBuilder();
            var buffer = new StringBuilder();
            bool flushed = false;

            // Tool-call text markers — if any of these appear early, keep buffering
            const int BufferThreshold = 50; // characters before we decide it's safe text

            await foreach (var chunk in _ollamaClient.ChatStreamAsync(
                _history.ToList(),
                _toolRegistry.GetToolDefinitions(),
                _config.OllamaModel,
                numCtx,
                numThread,
                numPredict,
                numBatch,
                keepAlive))
            {
                if (chunk.IsToolCall)
                    return (null, chunk.ToolResponse);

                if (chunk.Token != null)
                {
                    fullText.Append(chunk.Token);

                    if (!flushed)
                    {
                        buffer.Append(chunk.Token);
                        var bufStr = buffer.ToString();

                        // Check if buffer looks like a tool call
                        if (LooksLikeToolCall(bufStr))
                        {
                            // Keep buffering — don't emit anything to UI
                            continue;
                        }

                        // Once we have enough chars without tool call markers, flush everything
                        if (bufStr.Length >= BufferThreshold || !MightBeToolCall(bufStr))
                        {
                            flushed = true;
                            TokenReceived?.Invoke(bufStr);
                        }
                    }
                    else
                    {
                        TokenReceived?.Invoke(chunk.Token);
                    }
                }
            }

            // If we never flushed, check the complete text
            if (!flushed)
            {
                var completeText = fullText.ToString();
                // Don't emit tokens if it contains tool calls — the caller will handle it
                if (TryParseTextToolCalls(completeText).calls.Count > 0)
                    return (completeText, null);

                // Safe text that was just short — flush now
                TokenReceived?.Invoke(completeText);
            }

            return (fullText.ToString(), null);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>Definitely contains tool call patterns</summary>
    private static bool LooksLikeToolCall(string text)
        => text.Contains("tool_call", StringComparison.OrdinalIgnoreCase)
        || text.Contains("_icall_", StringComparison.OrdinalIgnoreCase)
        || (text.Contains("\"name\"") && text.Contains("\"arguments\""));

    /// <summary>Could still become a tool call (partial patterns in early tokens)</summary>
    private static bool MightBeToolCall(string text)
        => text.Contains("<tool", StringComparison.OrdinalIgnoreCase)
        || text.Contains("_ical", StringComparison.OrdinalIgnoreCase)
        || text.TrimStart().StartsWith("{")
        || text.TrimStart().StartsWith("<");
}
