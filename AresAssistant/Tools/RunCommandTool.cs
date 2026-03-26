using AresAssistant.Core;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace AresAssistant.Tools;

public class RunCommandTool : ITool
{
    public string Name => "run_command";
    public string Description => "Ejecuta un comando de consola de la lista de comandos permitidos.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["command"] = new() { Type = "string", Description = "Comando a ejecutar (sólo comandos de la lista de permitidos)" }
        },
        Required = new() { "command" }
    };

    private static readonly string[] AllowedPrefixes =
    {
        "ipconfig", "ping", "tracert", "nslookup", "netstat",
        "dir", "echo", "type", "tasklist", "systeminfo",
        "whoami", "hostname", "ver", "date", "time",
        "mkdir", "cd", "cls", "tree", "where", "wmic",
        "powershell", "python", "pip", "node", "npm", "npx",
        "git", "dotnet", "curl", "wget", "choco", "winget",
        "findstr", "more", "robocopy", "xcopy", "ren", "move",
        "copy", "attrib", "set ", "start", "notepad", "code"
    };

    private static readonly string[] BlockedPatterns =
    {
        "del ", "rmdir", " rd ", "/rd", "format ", "reg add", "reg delete", "reg import",
        "net user", "net localgroup", "sc delete", "sc stop", "bcdedit",
        "shutdown", "restart", "powershell -enc", "powershell -e ",
        "powershell -command", "powershell -c ", "pwsh -",
        "python -c", "python -c \"", "python -c '",
        "invoke-expression", "iex ", "invoke-webrequest",
        "remove-item", "rm -rf", "rm -r", "del /f", "del /s",
        "downloadstring", "downloadfile", "start-bitstransfer",
        "new-object net.webclient", "set-executionpolicy"
    };

    private const int TimeoutMs = 30_000;

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var command = args.TryGetValue("command", out var c) ? c.ToString().Trim() : "";

        var cmdLower = command.ToLowerInvariant();

        foreach (var blocked in BlockedPatterns)
        {
            if (cmdLower.Contains(blocked))
                return new ToolResult(false, $"Comando bloqueado por seguridad: contiene patrón '{blocked}'.");
        }

        bool allowed = AllowedPrefixes.Any(p => cmdLower.StartsWith(p));
        if (!allowed)
            return new ToolResult(false, "Comando no está en la lista de comandos permitidos.");

        try
        {
            var psi = new ProcessStartInfo("cmd.exe", $"/C {command}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi)!;
            using var cts = new CancellationTokenSource(TimeoutMs);

            var outputTask = proc.StandardOutput.ReadToEndAsync(cts.Token);
            var errorTask = proc.StandardError.ReadToEndAsync(cts.Token);

            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                return new ToolResult(false, "Comando cancelado: excedió el tiempo límite de 30 segundos.");
            }

            var output = await outputTask;
            var error = await errorTask;

            var result = string.IsNullOrWhiteSpace(output) ? error : output;
            return new ToolResult(true, result.Length > 3000 ? result[..3000] + "\n[...truncado]" : result);
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Error al ejecutar comando: {ex.Message}");
        }
    }
}
