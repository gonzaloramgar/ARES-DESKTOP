using System.Text;
using AresAssistant.Config;
using AresAssistant.Tools;

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
        var systemPrompt = BuildSystemPrompt();

        if (_history.Count == 0)
            _history.Add(new OllamaMessage("system", systemPrompt));
        else
            // Always refresh system prompt with the current config/personality
            _history.ReplaceSystemPrompt(systemPrompt);
    }

    private string BuildSystemPrompt()
    {
        var desktop   = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var userName  = Environment.UserName;

        var sb = new StringBuilder();
        sb.AppendLine($"Eres {_config.AssistantName}, un asistente de IA integrado en el sistema operativo del usuario.");
        sb.AppendLine("Respondes siempre en español. Eres directo, eficiente y ligeramente formal.");
        sb.AppendLine();

        sb.AppendLine("## USO DE HERRAMIENTAS — REGLA ABSOLUTA");
        sb.AppendLine("Tienes herramientas para controlar el ordenador. DEBES USARLAS siempre que el usuario pida una acción.");
        sb.AppendLine("NUNCA describas una acción sin ejecutarla con la herramienta correspondiente.");
        sb.AppendLine("NUNCA respondas 'voy a...', 'procedo a...', 'puedo...' sin haber llamado a la herramienta.");
        sb.AppendLine("Si el usuario pide:");
        sb.AppendLine("  - abrir algo            → usa open_app o open_folder");
        sb.AppendLine("  - cerrar algo            → usa close_app");
        sb.AppendLine("  - crear una carpeta      → usa create_folder");
        sb.AppendLine("  - leer un archivo        → usa read_file");
        sb.AppendLine("  - escribir un archivo    → usa write_file");
        sb.AppendLine("  - buscar en internet     → usa search_web o search_browser");
        sb.AppendLine("  - ejecutar un comando    → usa run_command");
        sb.AppendLine("  - hacer una captura      → usa screenshot");
        sb.AppendLine("  - escribir texto         → usa type_text");
        sb.AppendLine("  - información del sistema→ usa system_info");
        sb.AppendLine("  - subir/bajar volumen    → usa volume");
        sb.AppendLine("Usa la herramienta PRIMERO. Explica brevemente lo que hiciste DESPUÉS.");
        sb.AppendLine();

        sb.AppendLine("## LÍMITES");
        sb.AppendLine("Nunca borres archivos del sistema ni mates procesos críticos del SO.");
        sb.AppendLine("Si una acción es rechazada por el usuario, no la reintentas.");
        sb.AppendLine();

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

        return sb.ToString().Trim();
    }

    public async Task RunAsync(string userMessage)
    {
        _history.Add(new OllamaMessage("user", userMessage));
        // Keep context window lean: system prompt + last 30 messages
        _history.TrimToLast(30);

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
                _history.Add(new OllamaMessage("assistant", response.Message.Content ?? "")
                {
                    ToolCalls = response.Message.ToolCalls
                });

                foreach (var call in response.Message.ToolCalls)
                {
                    StatusChanged?.Invoke($"Ejecutando: {call.Function.Name}...");
                    var result = await _toolDispatcher.ExecuteAsync(call.Function.Name, call.Function.Arguments);
                    _history.Add(new OllamaMessage("tool", result)
                    {
                        ToolCallId = call.Id
                    });
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
