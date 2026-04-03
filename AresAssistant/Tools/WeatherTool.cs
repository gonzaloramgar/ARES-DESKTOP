using AresAssistant.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Globalization;

namespace AresAssistant.Tools;

/// <summary>
/// Herramienta para obtener el tiempo meteorológico actual y pronóstico
/// de 3 días usando coordenadas geográficas. Utiliza la API gratuita Open-Meteo.
/// </summary>
public class WeatherTool : ITool
{
    public string Name => "get_weather";
    public string Description =>
        "Obtiene el tiempo meteorológico actual y pronóstico de una ubicación. " +
        "Acepta latitud/longitud o ciudad. Si no se envía nada, intenta detectar ubicación automáticamente.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["latitude"] = new() { Type = "number", Description = "Latitud de la ubicación (opcional)" },
            ["longitude"] = new() { Type = "number", Description = "Longitud de la ubicación (opcional)" },
            ["city"] = new() { Type = "string", Description = "Ciudad o localidad (opcional)" }
        },
        Required = new()
    };

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        try
        {
            var lat = ReadDouble(args, "latitude", "latitud", "lat");
            var lon = ReadDouble(args, "longitude", "longitud", "lon", "lng");
            var city = ReadString(args, "city", "ciudad", "location", "ubicacion", "ubicación");

            LocationSnapshot? resolvedLocation = null;
            if (lat == null || lon == null)
            {
                if (!string.IsNullOrWhiteSpace(city))
                    resolvedLocation = await LocationResolver.ResolveCityAsync(city).ConfigureAwait(false);

                resolvedLocation ??= await LocationResolver.ResolveByIpAsync().ConfigureAwait(false);
                if (resolvedLocation == null)
                    return new ToolResult(false, "No pude resolver una ubicación válida. Prueba indicando una ciudad o usando get_location.");

                lat = resolvedLocation.Latitude;
                lon = resolvedLocation.Longitude;
            }

            // Open-Meteo — free, no API key, excellent weather data
            var latText = lat.Value.ToString(CultureInfo.InvariantCulture);
            var lonText = lon.Value.ToString(CultureInfo.InvariantCulture);

            var url = $"https://api.open-meteo.com/v1/forecast?" +
                      $"latitude={latText}&longitude={lonText}" +
                      $"&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m,wind_direction_10m" +
                      $"&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max" +
                      $"&timezone=auto&forecast_days=3";

            var json = await Http.GetStringAsync(url);
            var data = JObject.Parse(json);

            var current = data["current"];
            var daily = data["daily"];

            var weatherDesc = DecodeWeatherCode(current?["weather_code"]?.ToObject<int>() ?? -1);

            var result = new
            {
                ubicacion = new
                {
                    ciudad = resolvedLocation?.City ?? city ?? "Local",
                    latitud = lat,
                    longitud = lon,
                    source = resolvedLocation?.Source ?? "args"
                },
                actual = new
                {
                    condicion = weatherDesc,
                    temperatura = $"{current?["temperature_2m"]}°C",
                    sensacion_termica = $"{current?["apparent_temperature"]}°C",
                    humedad = $"{current?["relative_humidity_2m"]}%",
                    precipitacion = $"{current?["precipitation"]} mm",
                    viento = $"{current?["wind_speed_10m"]} km/h"
                },
                pronostico = BuildForecast(daily)
            };

            return new ToolResult(true, JsonConvert.SerializeObject(result, Formatting.Indented));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Error al obtener el tiempo: {ex.Message}");
        }
    }

    private static double? ReadDouble(Dictionary<string, JToken> args, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!args.TryGetValue(key, out var token) || token == null) continue;

            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
                return token.Value<double>();

            if (double.TryParse(token.ToString(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                return parsed;

            if (double.TryParse(token.ToString(), out parsed))
                return parsed;
        }

        return null;
    }

    private static string? ReadString(Dictionary<string, JToken> args, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!args.TryGetValue(key, out var token) || token == null)
                continue;

            var text = token.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return null;
    }

    private static object[] BuildForecast(JToken? daily)
    {
        if (daily == null) return [];

        var dates = daily["time"]?.ToObject<string[]>() ?? [];
        var maxTemps = daily["temperature_2m_max"]?.ToObject<double[]>() ?? [];
        var minTemps = daily["temperature_2m_min"]?.ToObject<double[]>() ?? [];
        var codes = daily["weather_code"]?.ToObject<int[]>() ?? [];
        var precip = daily["precipitation_sum"]?.ToObject<double[]>() ?? [];
        var precipProb = daily["precipitation_probability_max"]?.ToObject<int[]>() ?? [];

        var forecast = new List<object>();
        for (int i = 0; i < dates.Length; i++)
        {
            forecast.Add(new
            {
                fecha = dates[i],
                condicion = DecodeWeatherCode(i < codes.Length ? codes[i] : -1),
                maxima = i < maxTemps.Length ? $"{maxTemps[i]}°C" : "?",
                minima = i < minTemps.Length ? $"{minTemps[i]}°C" : "?",
                precipitacion = i < precip.Length ? $"{precip[i]} mm" : "?",
                probabilidad_lluvia = i < precipProb.Length ? $"{precipProb[i]}%" : "?"
            });
        }
        return forecast.ToArray();
    }

    private static string DecodeWeatherCode(int code) => code switch
    {
        0 => "Despejado",
        1 => "Mayormente despejado",
        2 => "Parcialmente nublado",
        3 => "Nublado",
        45 or 48 => "Niebla",
        51 or 53 or 55 => "Llovizna",
        56 or 57 => "Llovizna helada",
        61 or 63 or 65 => "Lluvia",
        66 or 67 => "Lluvia helada",
        71 or 73 or 75 => "Nieve",
        77 => "Granizo fino",
        80 or 81 or 82 => "Chubascos",
        85 or 86 => "Chubascos de nieve",
        95 => "Tormenta",
        96 or 99 => "Tormenta con granizo",
        _ => "Desconocido"
    };
}
