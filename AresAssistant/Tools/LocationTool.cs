using AresAssistant.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

/// <summary>
/// Herramienta para obtener la ubicación aproximada del usuario
/// mediante su IP pública (ciudad, región, país, coordenadas).
/// Utiliza múltiples proveedores gratuitos para mejorar la fiabilidad.
/// </summary>
public class LocationTool : ITool
{
    public string Name => "get_location";
    public string Description => "Obtiene la ubicación aproximada del usuario mediante su IP pública (ciudad, región, país, coordenadas). No requiere GPS.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new(),
        Required = new()
    };

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        try
        {
            var result = await LocationResolver.ResolveByIpAsync().ConfigureAwait(false);

            if (result == null)
                return new ToolResult(false, "No se pudo obtener la ubicación desde los servicios disponibles.");

            var payload = new
            {
                ciudad = result.City,
                region = result.Region,
                pais = result.Country,
                latitud = result.Latitude,
                longitud = result.Longitude,
                latitude = result.Latitude,
                longitude = result.Longitude,
                lat = result.Latitude,
                lon = result.Longitude,
                zona_horaria = result.Timezone,
                ip_publica = result.PublicIp,
                source = result.Source
            };

            return new ToolResult(true, JsonConvert.SerializeObject(payload, Formatting.Indented));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Error al obtener ubicación: {ex.Message}");
        }
    }
}
