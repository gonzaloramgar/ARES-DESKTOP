using System.Net;
using System.Text;
using AresAssistant.Config;
using AresAssistant.Tools;
using Newtonsoft.Json;

namespace AresAssistant.Core;

public sealed class LocalApiServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly ConfigManager _configManager;
    private readonly ToolRegistry _registry;
    private CancellationTokenSource? _cts;

    public int Port { get; }

    public LocalApiServer(int port, ConfigManager configManager, ToolRegistry registry)
    {
        Port = port;
        _configManager = configManager;
        _registry = registry;
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    public void Start()
    {
        if (_listener.IsListening) return;
        _cts = new CancellationTokenSource();
        _listener.Start();
        _ = Task.Run(() => LoopAsync(_cts.Token));
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listener.Stop(); } catch { }
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext? ctx = null;
            try
            {
                ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleAsync(ctx), ct);
            }
            catch
            {
                if (ct.IsCancellationRequested) break;
            }
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url?.AbsolutePath?.Trim('/').ToLowerInvariant() ?? string.Empty;

        try
        {
            if (ctx.Request.HttpMethod == "GET" && path == "health")
            {
                var checker = new OllamaHealthChecker();
                var report = await checker.CheckAsync(new OllamaClient(), _configManager.Config).ConfigureAwait(false);
                await WriteJsonAsync(ctx, 200, new { status = "ok", report = report.ToCompactText() }).ConfigureAwait(false);
                return;
            }

            if (ctx.Request.HttpMethod == "GET" && path == "tools")
            {
                var tools = _registry.GetAll().Select(t => new { name = t.Name, description = t.Description }).ToList();
                await WriteJsonAsync(ctx, 200, new { status = "ok", tools }).ConfigureAwait(false);
                return;
            }

            if (ctx.Request.HttpMethod == "GET" && path == "version")
            {
                await WriteJsonAsync(ctx, 200, new { status = "ok", version = App.AppVersion }).ConfigureAwait(false);
                return;
            }

            await WriteJsonAsync(ctx, 404, new { status = "error", message = "endpoint no encontrado" }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await WriteJsonAsync(ctx, 500, new { status = "error", message = ex.Message }).ConfigureAwait(false);
        }
    }

    private static async Task WriteJsonAsync(HttpListenerContext ctx, int statusCode, object payload)
    {
        var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
        var data = Encoding.UTF8.GetBytes(json);

        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.ContentLength64 = data.Length;
        await ctx.Response.OutputStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
        ctx.Response.Close();
    }

    public void Dispose()
    {
        Stop();
        _listener.Close();
        _cts?.Dispose();
    }
}
