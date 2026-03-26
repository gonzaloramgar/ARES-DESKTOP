using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Windows.Media.SpeechSynthesis;

namespace AresAssistant.Core;

/// <summary>
/// TTS engine with three tiers:
///   1. Piper  – offline neural TTS (auto-downloaded, ~60 MB)
///   2. Edge   – online neural via WebSocket (Microsoft Alvaro Neural, no account needed)
///   3. Local  – WinRT OneCore (always available, fallback)
///
/// Thread-safe: a new Speak() cancels any in-progress playback.
/// </summary>
public sealed class SpeechEngine : IDisposable
{
    // ─── Piper TTS ────────────────────────────────────────────────
    private const string PiperRelease   = "2023.11.14-2";
    private const string PiperZipUrl    =
        $"https://github.com/rhasspy/piper/releases/download/{PiperRelease}/piper_windows_amd64.zip";

    // es_ES-davefx-medium: Spain Spanish, male, natural cadence — sounds more natural than es_MX for Spain users
    private const string PiperVoiceName      = "es_ES-davefx-medium";
    private const string PiperVoiceBaseUrl   =
        "https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/es/es_ES/davefx/medium/";
    private static readonly string PiperVoiceUrl       = PiperVoiceBaseUrl + PiperVoiceName + ".onnx";
    private static readonly string PiperVoiceConfigUrl = PiperVoiceUrl + ".json";

    private static readonly string TtsDir    = Path.Combine("data", "tts");
    private static readonly string PiperExe  = Path.Combine(TtsDir, "piper", "piper.exe");
    private static readonly string VoiceModel  = Path.Combine(TtsDir, $"{PiperVoiceName}.onnx");
    private static readonly string VoiceConfig = VoiceModel + ".json";

    // ─── Edge TTS ─────────────────────────────────────────────────
    private const string EdgeEndpoint =
        "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1";
    private const string TrustedToken    = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const string EdgeOutputFormat = "audio-24khz-96kbitrate-mono-mp3";

    // Primary: Alvaro – natural conversational male (Spain)
    // Fallback: Dario – slightly different timbre
    private const string EdgeVoicePrimary  = "es-ES-AlvaroNeural";
    private const string EdgeVoiceFallback = "es-ES-DarioNeural";

    // ─── State ────────────────────────────────────────────────────
    private volatile bool _piperReady;
    private volatile bool _piperDownloading;
    private WaveOutEvent? _player;
    private CancellationTokenSource? _cts;
    private readonly object _lock = new();
    private bool _enabled;
    private float _volume = 0.5f;

    /// <summary>Fired after synthesis completes, reporting which engine was used ("piper", "edge", "local").</summary>
    public event Action<string>? EngineUsed;

    public string LastEngine      { get; private set; } = "";
    public bool   PiperReady      => _piperReady;
    public bool   PiperDownloading => _piperDownloading;

    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; if (!value) Stop(); }
    }

    /// <summary>Playback volume 0.0–1.0. Applied to each new playback.</summary>
    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0f, 1f);
    }

    public SpeechEngine()
    {
        _piperReady = File.Exists(PiperExe) && File.Exists(VoiceModel);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Public API
    // ═══════════════════════════════════════════════════════════════

    public void Speak(string text)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(text)) return;
        Stop();

        var cts = new CancellationTokenSource();
        lock (_lock) { _cts = cts; }

        _ = Task.Run(() => SpeakInternalAsync(CleanForSpeech(text), cts.Token));
    }

    public void Stop()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts = null;
            if (_player != null)
            {
                try { _player.Stop(); } catch { }
                _player.Dispose();
                _player = null;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Internal orchestration
    // ═══════════════════════════════════════════════════════════════

    private async Task SpeakInternalAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        byte[]? audio = null;

        // 1️⃣ Piper — offline neural (best for privacy / no internet)
        if (_piperReady)
        {
            try
            {
                audio = await SynthesizePiperAsync(text, ct);
                if (audio is { Length: > 0 })
                {
                    LastEngine = "piper";
                    EngineUsed?.Invoke("piper");
                }
            }
            catch (OperationCanceledException) { return; }
            catch { /* process failed, fall through */ }
        }

        // 2️⃣ Edge TTS — online neural (excellent quality when available)
        if (audio == null || audio.Length == 0)
        {
            // Kick off Piper download while Edge synthesizes
            if (!_piperReady && !_piperDownloading)
                _ = Task.Run(DownloadPiperAsync);

            try
            {
                audio = await SynthesizeEdgeAsync(text, EdgeVoicePrimary, ct);
                if (audio is { Length: > 0 })
                {
                    LastEngine = "edge";
                    EngineUsed?.Invoke("edge");
                }
            }
            catch (OperationCanceledException) { return; }
            catch { /* timeout or offline */ }

            // Retry with fallback voice if primary failed
            if ((audio == null || audio.Length == 0) && !ct.IsCancellationRequested)
            {
                try
                {
                    audio = await SynthesizeEdgeAsync(text, EdgeVoiceFallback, ct);
                    if (audio is { Length: > 0 })
                    {
                        LastEngine = "edge-fallback";
                        EngineUsed?.Invoke("edge");
                    }
                }
                catch (OperationCanceledException) { return; }
                catch { /* still offline */ }
            }
        }

        // 3️⃣ Local WinRT — always available, basic quality
        if (audio == null || audio.Length == 0)
        {
            if (!_piperReady && !_piperDownloading)
                _ = Task.Run(DownloadPiperAsync);

            try
            {
                audio = await SynthesizeLocalAsync(text, ct);
                if (audio is { Length: > 0 })
                {
                    LastEngine = "local";
                    EngineUsed?.Invoke("local");
                }
            }
            catch (OperationCanceledException) { return; }
            catch { return; }
        }

        if (audio == null || audio.Length == 0 || ct.IsCancellationRequested) return;
        await PlayAudioAsync(audio, ct);
    }

    // ═══════════════════════════════════════════════════════════════
    //  1. Piper TTS
    // ═══════════════════════════════════════════════════════════════

    private static async Task<byte[]?> SynthesizePiperAsync(string text, CancellationToken ct)
    {
        var wavFile = Path.Combine(Path.GetTempPath(), $"ares_tts_{Guid.NewGuid():N}.wav");
        try
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName  = PiperExe,
                Arguments = $"--model \"{VoiceModel}\" --output_file \"{wavFile}\" --length_scale 1.05",
                RedirectStandardInput  = true,
                RedirectStandardError  = true,
                UseShellExecute  = false,
                CreateNoWindow   = true,
            };
            proc.Start();

            await proc.StandardInput.WriteLineAsync(text);
            proc.StandardInput.Close();

            using (ct.Register(() => { try { proc.Kill(); } catch { } }))
                await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0 || !File.Exists(wavFile)) return null;
            return await File.ReadAllBytesAsync(wavFile, ct);
        }
        finally
        {
            try { if (File.Exists(wavFile)) File.Delete(wavFile); } catch { }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Piper auto-download
    // ═══════════════════════════════════════════════════════════════

    public async Task DownloadPiperAsync()
    {
        if (_piperReady || _piperDownloading) return;
        _piperDownloading = true;

        try
        {
            Directory.CreateDirectory(TtsDir);
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };

            if (!File.Exists(PiperExe))
            {
                var zipPath = Path.Combine(TtsDir, "piper.zip");
                using (var resp = await http.GetAsync(PiperZipUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    resp.EnsureSuccessStatusCode();
                    using var fs = File.Create(zipPath);
                    await resp.Content.CopyToAsync(fs);
                }
                ZipFile.ExtractToDirectory(zipPath, TtsDir, overwriteFiles: true);
                try { File.Delete(zipPath); } catch { }
            }

            if (!File.Exists(VoiceModel))
            {
                using var resp = await http.GetAsync(PiperVoiceUrl, HttpCompletionOption.ResponseHeadersRead);
                resp.EnsureSuccessStatusCode();
                using var fs = File.Create(VoiceModel);
                await resp.Content.CopyToAsync(fs);
            }

            if (!File.Exists(VoiceConfig))
            {
                var bytes = await http.GetByteArrayAsync(PiperVoiceConfigUrl);
                await File.WriteAllBytesAsync(VoiceConfig, bytes);
            }

            _piperReady = File.Exists(PiperExe) && File.Exists(VoiceModel);
        }
        catch { /* retry on next speak */ }
        finally { _piperDownloading = false; }
    }

    // ═══════════════════════════════════════════════════════════════
    //  2. Edge TTS — online neural via WebSocket
    // ═══════════════════════════════════════════════════════════════

    private static async Task<byte[]?> SynthesizeEdgeAsync(string text, string voice, CancellationToken ct)
    {
        var connId    = Guid.NewGuid().ToString("N").ToLower();
        var requestId = Guid.NewGuid().ToString("N").ToLower();

        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Origin",
            "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
        ws.Options.SetRequestHeader("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0");

        var uri = new Uri($"{EdgeEndpoint}?TrustedClientToken={TrustedToken}&ConnectionId={connId}");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));

        await ws.ConnectAsync(uri, timeout.Token);

        // Speech config
        var configMsg =
            "Content-Type:application/json; charset=utf-8\r\n" +
            "Path:speech.config\r\n\r\n" +
            "{\"context\":{\"synthesis\":{\"audio\":{" +
            "\"metadataoptions\":{\"sentenceBoundaryEnabled\":\"false\",\"wordBoundaryEnabled\":\"false\"}," +
            $"\"outputFormat\":\"{EdgeOutputFormat}\"" +
            "}}}}";
        await WsSendAsync(ws, configMsg, timeout.Token);

        // SSML with express-as chat style + prosody for natural, conversational delivery
        var escaped = System.Security.SecurityElement.Escape(text);
        var ssml =
            $"<speak version='1.0' " +
            $"xmlns='http://www.w3.org/2001/10/synthesis' " +
            $"xmlns:mstts='https://www.w3.org/2001/mstts' " +
            $"xml:lang='es-ES'>" +
            $"<voice name='{voice}'>" +
            $"<mstts:express-as style='chat'>" +
            $"<prosody rate='0.92' pitch='-3%'>{escaped}</prosody>" +
            $"</mstts:express-as>" +
            $"</voice>" +
            $"</speak>";

        var ssmlMsg =
            $"X-RequestId:{requestId}\r\n" +
            "Content-Type:application/ssml+xml\r\n" +
            $"X-Timestamp:{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}\r\n" +
            "Path:ssml\r\n\r\n" + ssml;
        await WsSendAsync(ws, ssmlMsg, timeout.Token);

        // Collect audio chunks
        using var audioStream = new MemoryStream();
        var buffer = new byte[32768];

        while (ws.State == WebSocketState.Open)
        {
            timeout.Token.ThrowIfCancellationRequested();
            var result = await ws.ReceiveAsync(buffer, timeout.Token);

            if (result.MessageType == WebSocketMessageType.Close) break;

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                using var msgBuf = new MemoryStream();
                msgBuf.Write(buffer, 0, result.Count);
                while (!result.EndOfMessage)
                {
                    result = await ws.ReceiveAsync(buffer, timeout.Token);
                    msgBuf.Write(buffer, 0, result.Count);
                }
                var data = msgBuf.ToArray();
                if (data.Length < 2) continue;
                int headerLen  = (data[0] << 8) | data[1];
                int audioOffset = 2 + headerLen;
                if (audioOffset < data.Length)
                    audioStream.Write(data, audioOffset, data.Length - audioOffset);
            }
            else if (result.MessageType == WebSocketMessageType.Text)
            {
                using var msgBuf = new MemoryStream();
                msgBuf.Write(buffer, 0, result.Count);
                while (!result.EndOfMessage)
                {
                    result = await ws.ReceiveAsync(buffer, timeout.Token);
                    msgBuf.Write(buffer, 0, result.Count);
                }
                if (Encoding.UTF8.GetString(msgBuf.ToArray()).Contains("Path:turn.end"))
                    break;
            }
        }

        try
        {
            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
        catch { }

        return audioStream.Length > 0 ? audioStream.ToArray() : null;
    }

    private static async Task WsSendAsync(ClientWebSocket ws, string msg, CancellationToken ct)
        => await ws.SendAsync(Encoding.UTF8.GetBytes(msg), WebSocketMessageType.Text, true, ct);

    // ═══════════════════════════════════════════════════════════════
    //  3. Local WinRT — offline fallback
    // ═══════════════════════════════════════════════════════════════

    private static async Task<byte[]?> SynthesizeLocalAsync(string text, CancellationToken ct)
    {
        using var synth = new SpeechSynthesizer();
        var voices = SpeechSynthesizer.AllVoices;

        // Prefer newer neural voices (Windows 11) over legacy robotic voices
        var chosen =
            voices.FirstOrDefault(v => v.DisplayName.Contains("Elvira", StringComparison.OrdinalIgnoreCase))
            ?? voices.FirstOrDefault(v => v.DisplayName.Contains("Pablo", StringComparison.OrdinalIgnoreCase))
            ?? voices.FirstOrDefault(v => v.DisplayName.Contains("Alvaro", StringComparison.OrdinalIgnoreCase))
            ?? voices.FirstOrDefault(v =>
                v.Language.StartsWith("es-ES", StringComparison.OrdinalIgnoreCase)
                && v.Gender == VoiceGender.Male)
            ?? voices.FirstOrDefault(v =>
                v.Language.StartsWith("es", StringComparison.OrdinalIgnoreCase))
            ?? voices.FirstOrDefault(v =>
                v.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase));

        if (chosen != null) synth.Voice = chosen;

        var lang    = synth.Voice?.Language ?? "es-ES";
        var escaped = System.Security.SecurityElement.Escape(text);
        // Slightly slower rate makes WinRT voices sound less robotic
        var ssml =
            $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{lang}'>" +
            $"<prosody rate='0.85' pitch='-5%'><break time='150ms'/>{escaped}</prosody>" +
            $"</speak>";

        var stream = await synth.SynthesizeSsmlToStreamAsync(ssml).AsTask(ct);

        using var ms = new MemoryStream();
        using (var input = stream.AsStreamForRead())
            await input.CopyToAsync(ms, 81920, ct);
        return ms.ToArray();
    }

    // ═══════════════════════════════════════════════════════════════
    //  Playback
    // ═══════════════════════════════════════════════════════════════

    private async Task PlayAudioAsync(byte[] audio, CancellationToken ct)
    {
        var ms = new MemoryStream(audio);
        StreamMediaFoundationReader? reader = null;
        WaveOutEvent?               player = null;

        try
        {
            reader = new StreamMediaFoundationReader(ms);

            // Prepend 250 ms of silence before the speech so the audio device has time
            // to fully initialize before the first syllable. Without this the OS audio
            // engine eats the first ~100-200 ms while it opens the device.
            var withSilence = new OffsetSampleProvider(reader.ToSampleProvider())
            {
                DelayBy = TimeSpan.FromMilliseconds(250)
            };

            player = new WaveOutEvent { DesiredLatency = 300, NumberOfBuffers = 3, Volume = _volume };
            player.Init(withSilence);

            lock (_lock)
            {
                if (ct.IsCancellationRequested)
                {
                    player.Dispose(); reader.Dispose(); ms.Dispose();
                    return;
                }
                _player = player;
            }

            var tcs = new TaskCompletionSource();
            player.PlaybackStopped += (_, _) => tcs.TrySetResult();
            player.Play();

            using (ct.Register(() => { try { player.Stop(); } catch { } }))
                await tcs.Task;
        }
        catch (OperationCanceledException) { }
        catch { }
        finally
        {
            lock (_lock) { if (_player == player) _player = null; }
            player?.Dispose();
            reader?.Dispose();
            ms.Dispose();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Text cleanup
    // ═══════════════════════════════════════════════════════════════

    private static string CleanForSpeech(string text)
    {
        // Remove code blocks
        text = Regex.Replace(text, @"```[\s\S]*?```", "código omitido");
        text = Regex.Replace(text, @"`[^`\n]+`", "");

        // Remove markdown formatting
        text = text.Replace("**", "").Replace("__", "").Replace("*", "").Replace("_", " ");
        text = Regex.Replace(text, @"^#{1,6}\s+", "", RegexOptions.Multiline);

        // Replace markdown links with just the label
        text = Regex.Replace(text, @"\[([^\]]+)\]\([^\)]+\)", "$1");

        // Replace bullet/list markers with natural pauses
        text = Regex.Replace(text, @"^\s*[-•]\s+", ", ", RegexOptions.Multiline);
        text = Regex.Replace(text, @"^\s*\d+\.\s+", ", ", RegexOptions.Multiline);

        // Remove URLs
        text = Regex.Replace(text, @"https?://\S+", "el enlace");

        // Remove emojis and special Unicode symbols
        text = Regex.Replace(text, @"[\p{So}\p{Sm}\p{Sk}\p{Sc}]", "");

        // Collapse whitespace
        text = Regex.Replace(text, @"\s+", " ").Trim();

        // Truncate very long texts to avoid 30-second TTS responses
        if (text.Length > 600)
        {
            var cutoff = text.LastIndexOf('.', 600);
            text = cutoff > 200 ? text[..(cutoff + 1)] + " He resumido la respuesta." : text[..600];
        }

        return text;
    }

    public void Dispose() => Stop();
}
