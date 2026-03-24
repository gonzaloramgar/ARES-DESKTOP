using AresAssistant.Config;
using AresAssistant.Models;
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

    public event Action<string>? ResponseReceived;
    public event Action<string>? StatusChanged;

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
        if (_history.Count > 0) return;

        var systemPrompt = BuildSystemPrompt();
        _history.Add(new OllamaMessage("system", systemPrompt));
    }

    private string BuildSystemPrompt()
    {
        var desktop   = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var userName  = Environment.UserName;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Eres {_config.AssistantName}, un asistente de IA integrado en el sistema operativo del usuario.");
        sb.AppendLine("Respondes siempre en español. Eres directo, eficiente y ligeramente formal.");
        sb.AppendLine("Tienes acceso a herramientas para controlar el ordenador del usuario.");
        sb.AppendLine("Cuando una acción es rechazada por el usuario, no la reintentas y lo indicas claramente.");
        sb.AppendLine("Nunca borres archivos ni mates procesos del sistema.");
        sb.AppendLine();
        sb.AppendLine("RUTAS IMPORTANTES DEL SISTEMA:");
        sb.AppendLine($"  Usuario: {userName}");
        sb.AppendLine($"  Escritorio: {desktop}");
        sb.AppendLine($"  Documentos: {documents}");
        sb.AppendLine($"  Descargas: {downloads}");
        sb.AppendLine("Para crear carpetas usa la herramienta 'create_folder'. Puedes usar alias: Desktop, Documents, Downloads, Pictures, Music, Videos.");
        sb.AppendLine("Para crear carpetas en el escritorio usa path='Desktop/NombreCarpeta'.");
        sb.AppendLine();

        sb.AppendLine(_config.Personality switch
        {
            "casual" => "Usa un tono informal y cercano.",
            "sarcastico" => "Puedes ser levemente sarcástico y con humor.",
            "tecnico" => "Usa terminología técnica precisa en tus respuestas.",
            _ => ""
        });

        sb.AppendLine(_config.ResponseLength switch
        {
            "conciso" => "Responde siempre de forma muy breve, máximo 2 frases.",
            "detallado" => "Explica tus acciones y razonamientos con detalle.",
            _ => ""
        });

        return sb.ToString().Trim();
    }

    public async Task RunAsync(string userMessage)
    {
        _history.Add(new OllamaMessage("user", userMessage));

        int iterations = 0;

        while (iterations < MaxIterations)
        {
            iterations++;

            OllamaResponse response;
            try
            {
                StatusChanged?.Invoke("Pensando...");
                response = await _ollamaClient.ChatAsync(
                    _history.ToList(),
                    _toolRegistry.GetToolDefinitions(),
                    _config.OllamaModel);
            }
            catch (Exception ex)
            {
                var errMsg = $"Error al conectar con Ollama: {ex.Message}";
                ResponseReceived?.Invoke(errMsg);
                return;
            }

            if (response.Message.ToolCalls?.Count > 0)
            {
                _history.Add(new OllamaMessage("assistant", response.Message.Content ?? "")
                {
                    ToolCalls = response.Message.ToolCalls
                });

                foreach (var call in response.Message.ToolCalls)
                {
                    StatusChanged?.Invoke($"Ejecutando: {call.Function.Name}...");
                    var argsDict = call.Function.Arguments
                        .ToDictionary(kv => kv.Key, kv => (JToken)kv.Value);

                    var result = await _toolDispatcher.ExecuteAsync(call.Function.Name, argsDict);
                    _history.Add(new OllamaMessage("tool", result));
                }
                // loop back to get AI's text response
            }
            else
            {
                var content = response.Message.Content ?? "";
                _history.Add(new OllamaMessage("assistant", content));
                StatusChanged?.Invoke("");
                ResponseReceived?.Invoke(content);
                return;
            }
        }

        var fallback = "ARES: He alcanzado el límite de acciones consecutivas. Por favor, reformula tu petición.";
        ResponseReceived?.Invoke(fallback);
    }
}
