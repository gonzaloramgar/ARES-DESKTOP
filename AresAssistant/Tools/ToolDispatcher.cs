using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

/// <summary>
/// Ejecuta herramientas aplicando el sistema de permisos y registro de acciones.
/// Bloquea acciones prohibidas, solicita confirmación para las sensibles
/// y registra toda ejecución en el ActionLogger.
/// </summary>
public class ToolDispatcher
{
    private readonly ToolRegistry _registry;
    private readonly PermissionManager _permissionManager;
    private readonly ActionLogger _logger;

    public event Func<string, Dictionary<string, JToken>, Task<bool>>? ConfirmationRequested;

    public ToolDispatcher(ToolRegistry registry, PermissionManager permissionManager, ActionLogger logger)
    {
        _registry = registry;
        _permissionManager = permissionManager;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(string toolName, Dictionary<string, JToken> args)
    {
        var tool = _registry.Get(toolName);
        if (tool == null)
            return "Error: herramienta desconocida.";

        var permission = _permissionManager.GetLevel(toolName, args);

        if (permission == PermissionLevel.Blocked)
        {
            _logger.Log(permission, toolName, args);
            return "Acción bloqueada por el sistema de seguridad.";
        }

        if (permission == PermissionLevel.Confirm)
        {
            bool approved = true;
            if (ConfirmationRequested != null)
                approved = await ConfirmationRequested(toolName, args);

            if (!approved)
            {
                _logger.Log(permission, toolName, new { cancelled = true });
                return "Acción cancelada por el usuario.";
            }
        }

        _logger.Log(permission, toolName, args);

        try
        {
            var result = await tool.ExecuteAsync(args).ConfigureAwait(false);
            return result.Message;
        }
        catch (Exception ex)
        {
            return $"Error al ejecutar {toolName}: {ex.Message}";
        }
    }
}
