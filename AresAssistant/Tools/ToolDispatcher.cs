using AresAssistant.Core;
using AresAssistant;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

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
    private readonly ReliabilityTelemetryStore? _telemetry;

    public event Func<string, Dictionary<string, JToken>, Task<bool>>? ConfirmationRequested;

    public ToolDispatcher(
        ToolRegistry registry,
        PermissionManager permissionManager,
        ActionLogger logger,
        ReliabilityTelemetryStore? telemetry = null)
    {
        _registry = registry;
        _permissionManager = permissionManager;
        _logger = logger;
        _telemetry = telemetry;
    }

    public async Task<string> ExecuteAsync(string toolName, Dictionary<string, JToken> args)
    {
        args ??= new Dictionary<string, JToken>();
        App.WriteAction("ToolDispatcher", "Execute.Start", new { toolName, argsCount = args.Count });

        var tool = _registry.Get(toolName);
        if (tool == null)
        {
            App.WriteAction("ToolDispatcher", "Execute.UnknownTool", new { toolName }, "WARN");
            return "Error: herramienta desconocida.";
        }

        var permission = _permissionManager.GetLevel(toolName, args);

        if (permission == PermissionLevel.Blocked)
        {
            _logger.Log(permission, toolName, args);
            App.WriteAction("ToolDispatcher", "Execute.Blocked", new { toolName, permission = permission.ToString() }, "WARN");
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
                App.WriteAction("ToolDispatcher", "Execute.CancelledByUser", new { toolName });
                return "Acción cancelada por el usuario.";
            }
        }

        _logger.Log(permission, toolName, args);
        var sw = Stopwatch.StartNew();

        try
        {
            var result = await tool.ExecuteAsync(args).ConfigureAwait(false);
            _telemetry?.RecordTool(toolName, result.Success, sw.ElapsedMilliseconds);
            App.WriteAction("ToolDispatcher", "Execute.Result", new { toolName, result.Success, elapsedMs = sw.ElapsedMilliseconds });
            return result.Message;
        }
        catch (Exception ex)
        {
            _telemetry?.RecordTool(toolName, false, sw.ElapsedMilliseconds);
            App.WriteAction("ToolDispatcher", "Execute.Exception", new { toolName, elapsedMs = sw.ElapsedMilliseconds, ex.Message }, "ERROR");
            App.WriteCrash($"ToolDispatcher.Execute.{toolName}", ex);
            return $"Error al ejecutar {toolName}: {ex.Message}";
        }
    }
}
