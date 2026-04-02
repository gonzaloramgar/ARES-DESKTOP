using AresAssistant.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;

namespace AresAssistant.Tools;

/// <summary>
/// Herramienta para obtener la ubicación aproximada del usuario
/// mediante su IP pública (ciudad, región, país, coordenadas).
/// Utiliza la API gratuita ip-api.com sin necesidad de API key.
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

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        try
        {
            var result = await TryIpWhoIsAsync().ConfigureAwait(false)
                         ?? await TryIpApiAsync().ConfigureAwait(false);

            if (result == null)
                return new ToolResult(false, "No se pudo obtener la ubicación desde los servicios disponibles.");

            return new ToolResult(true, JsonConvert.SerializeObject(result, Formatting.Indented));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Error al obtener ubicación: {ex.Message}");
        }
    }

    private static async Task<object?> TryIpWhoIsAsync()
    {
        try
        {
            var json = await Http.GetStringAsync("https://ipwho.is/").ConfigureAwait(false);
            var data = JObject.Parse(json);
            if (data["success"]?.ToObject<bool>() != true) return null;

            var lat = data["latitude"]?.ToObject<double>();
            var lon = data["longitude"]?.ToObject<double>();
            if (lat == null || lon == null) return null;

            return new
            {
                ciudad = data["city"]?.ToString(),
                region = data["region"]?.ToString(),
                pais = data["country"]?.ToString(),
                latitud = lat,
                longitud = lon,
                latitude = lat,
                longitude = lon,
                lat,
                lon,
                zona_horaria = data["timezone"]?["id"]?.ToString(),
                ip_publica = data["ip"]?.ToString(),
                source = "ipwho.is"
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<object?> TryIpApiAsync()
    {
        try
        {
            var json = await Http.GetStringAsync("http://ip-api.com/json/?fields=status,message,country,regionName,city,lat,lon,timezone,query&lang=es").ConfigureAwait(false);
            var data = JObject.Parse(json);
            if (data["status"]?.ToString() != "success") return null;

            var lat = data["lat"]?.ToObject<double>();
            var lon = data["lon"]?.ToObject<double>();
            if (lat == null || lon == null) return null;

            return new
            {
                ciudad = data["city"]?.ToString(),
                region = data["regionName"]?.ToString(),
                pais = data["country"]?.ToString(),
                latitud = lat,
                longitud = lon,
                latitude = lat,
                longitude = lon,
                lat,
                lon,
                zona_horaria = data["timezone"]?.ToString(),
                ip_publica = data["query"]?.ToString(),
                source = "ip-api"
            };
        }
        catch
        {
            return null;
        }
    }
}
