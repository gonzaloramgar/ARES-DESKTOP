using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Core;

public record ReleaseInfo(string TagName, string DownloadUrl, string Notes);

public static class UpdateChecker
{
    private const string ApiUrl = "https://api.github.com/repos/gonzaloramgar/ARES/releases/latest";

    public static async Task<ReleaseInfo?> CheckAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ARES", "1.0"));

        var json = await http.GetStringAsync(ApiUrl);
        var obj = JObject.Parse(json);

        var tag = obj["tag_name"]?.Value<string>();
        if (tag == null) return null;

        var assets = obj["assets"] as JArray;
        var zip = assets?.FirstOrDefault(a => a["name"]?.Value<string>()?.EndsWith(".zip") == true);
        var url = zip?["browser_download_url"]?.Value<string>();
        if (url == null) return null;

        // Trim changelog to first 5 lines
        var body = obj["body"]?.Value<string>() ?? "";
        var lines = body.Split('\n').Take(5);
        var notes = string.Join('\n', lines).Trim();

        return new ReleaseInfo(tag, url, notes);
    }

    public static bool IsNewer(string remoteTag)
    {
        var remote = remoteTag.TrimStart('v');
        var current = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        return Version.TryParse(remote, out var rv) &&
               Version.TryParse(current, out var cv) &&
               rv > cv;
    }
}
