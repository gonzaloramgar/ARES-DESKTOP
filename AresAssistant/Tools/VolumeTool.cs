using AresAssistant.Core;
using NAudio.CoreAudioApi;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

public class VolumeTool : ITool
{
    public string Name => "set_volume";
    public string Description => "Ajusta el volumen del sistema o lo silencia.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["level"] = new() { Type = "integer", Description = "Nivel de volumen de 0 a 100" },
            ["mute"] = new() { Type = "boolean", Description = "Silenciar el audio", Default = false }
        },
        Required = new() { "level" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        try
        {
            var level = args.TryGetValue("level", out var l) ? l.Value<int>() : 50;
            var mute = args.TryGetValue("mute", out var m) && m.Value<bool>();

            level = Math.Clamp(level, 0, 100);

            using var device = new MMDeviceEnumerator()
                .GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            device.AudioEndpointVolume.MasterVolumeLevelScalar = level / 100f;
            device.AudioEndpointVolume.Mute = mute;

            return Task.FromResult(new ToolResult(true,
                mute ? "Audio silenciado." : $"Volumen ajustado a {level}%."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"Error al ajustar volumen: {ex.Message}"));
        }
    }
}
