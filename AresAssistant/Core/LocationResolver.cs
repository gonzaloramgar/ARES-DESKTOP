using System.Globalization;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Core;

public sealed class LocationSnapshot
{
    public string City { get; init; } = "Local";
    public string Region { get; init; } = "";
    public string Country { get; init; } = "";
    public string Timezone { get; init; } = "";
    public string PublicIp { get; init; } = "";
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string Source { get; init; } = "";
}

public static class LocationResolver
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    static LocationResolver()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("AresAssistant/1.3.5");
    }

    public static async Task<LocationSnapshot?> ResolveByIpAsync()
    {
        var fromIpWho = await TryIpWhoIsAsync().ConfigureAwait(false);
        if (fromIpWho != null)
            return fromIpWho;

        return await TryIpApiCoAsync().ConfigureAwait(false);
    }

    public static async Task<LocationSnapshot?> ResolveCityAsync(string city)
    {
        if (string.IsNullOrWhiteSpace(city))
            return null;

        try
        {
            var encoded = Uri.EscapeDataString(city.Trim());
            var url = $"https://geocoding-api.open-meteo.com/v1/search?name={encoded}&count=1&language=es&format=json";
            var json = await Http.GetStringAsync(url).ConfigureAwait(false);
            var data = JObject.Parse(json);
            var first = data["results"]?.FirstOrDefault();
            if (first == null)
                return null;

            var lat = first["latitude"]?.ToObject<double>();
            var lon = first["longitude"]?.ToObject<double>();
            if (lat == null || lon == null)
                return null;

            return new LocationSnapshot
            {
                City = first["name"]?.ToString() ?? city,
                Region = first["admin1"]?.ToString() ?? "",
                Country = first["country"]?.ToString() ?? "",
                Timezone = first["timezone"]?.ToString() ?? "",
                Latitude = lat.Value,
                Longitude = lon.Value,
                Source = "open-meteo-geocoding"
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<LocationSnapshot?> TryIpWhoIsAsync()
    {
        try
        {
            var raw = await Http.GetStringAsync("https://ipwho.is/").ConfigureAwait(false);
            var data = JObject.Parse(raw);
            if (data["success"]?.ToObject<bool>() != true)
                return null;

            var lat = data["latitude"]?.ToObject<double>();
            var lon = data["longitude"]?.ToObject<double>();
            if (lat == null || lon == null)
                return null;

            return new LocationSnapshot
            {
                City = data["city"]?.ToString() ?? "Local",
                Region = data["region"]?.ToString() ?? "",
                Country = data["country"]?.ToString() ?? "",
                Timezone = data["timezone"]?["id"]?.ToString() ?? "",
                PublicIp = data["ip"]?.ToString() ?? "",
                Latitude = lat.Value,
                Longitude = lon.Value,
                Source = "ipwho.is"
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<LocationSnapshot?> TryIpApiCoAsync()
    {
        try
        {
            var raw = await Http.GetStringAsync("https://ipapi.co/json/").ConfigureAwait(false);
            var data = JObject.Parse(raw);

            var lat = ReadDouble(data["latitude"]);
            var lon = ReadDouble(data["longitude"]);
            if (lat == null || lon == null)
                return null;

            return new LocationSnapshot
            {
                City = data["city"]?.ToString() ?? "Local",
                Region = data["region"]?.ToString() ?? "",
                Country = data["country_name"]?.ToString() ?? "",
                Timezone = data["timezone"]?.ToString() ?? "",
                PublicIp = data["ip"]?.ToString() ?? "",
                Latitude = lat.Value,
                Longitude = lon.Value,
                Source = "ipapi.co"
            };
        }
        catch
        {
            return null;
        }
    }

    private static double? ReadDouble(JToken? token)
    {
        if (token == null)
            return null;

        if (token.Type is JTokenType.Float or JTokenType.Integer)
            return token.Value<double>();

        var text = token.ToString();
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;

        return double.TryParse(text, out value) ? value : null;
    }
}
