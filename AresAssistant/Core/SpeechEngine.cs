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

    // Male: es_ES-davefx-medium — Spain Spanish, natural cadence
    private const string PiperVoiceMaleName    = "es_ES-davefx-medium";
    private const string PiperVoiceMaleBaseUrl =
        "https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/es/es_ES/davefx/medium/";

    // Female: es_ES-sharvard-medium — Spain Spanish female, same quality tier as davefx
    private const string PiperVoiceFemaleName    = "es_ES-sharvard-medium";
    private const string PiperVoiceFemaleBaseUrl =
        "https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/es/es_ES/sharvard/medium/";

    private static readonly string TtsDir   = Path.Combine("data", "tts");
    private static readonly string PiperExe = Path.Combine(TtsDir, "piper", "piper.exe");

    private static readonly string VoiceModelMale    = Path.Combine(TtsDir, $"{PiperVoiceMaleName}.onnx");
    private static readonly string VoiceConfigMale   = VoiceModelMale + ".json";
    private static readonly string VoiceModelFemale  = Path.Combine(TtsDir, $"{PiperVoiceFemaleName}.onnx");
    private static readonly string VoiceConfigFemale = VoiceModelFemale + ".json";

    // ─── Edge TTS ─────────────────────────────────────────────────
    private const string EdgeEndpoint =
        "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1";
    private const string TrustedToken    = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const string EdgeOutputFormat = "audio-24khz-96kbitrate-mono-mp3";

    // Male voices
    private const string EdgeVoiceMalePrimary   = "es-ES-AlvaroNeural";
    private const string EdgeVoiceMaleFallback  = "es-ES-DarioNeural";
    // Female voices — DaliaNeural (MX) is the most reliable female Spanish voice on Edge TTS
    private const string EdgeVoiceFemalePrimary  = "es-MX-DaliaNeural";
    private const string EdgeVoiceFemaleFallback = "es-ES-ElviraNeural";

    // ─── State ────────────────────────────────────────────────────
    private volatile bool _piperReadyMale;
    private volatile bool _piperReadyFemale;
    private volatile bool _piperDownloadingMale;
    private volatile bool _piperDownloadingFemale;
    private WaveOutEvent? _player;
    private CancellationTokenSource? _cts;
    private readonly object _lock = new();
    private bool _enabled;
    private float _volume = 0.5f;
    private string _voiceGender = "masculino";

    /// <summary>Fired after synthesis completes, reporting which engine was used ("piper", "edge", "local").</summary>
    public event Action<string>? EngineUsed;

    public string LastEngine       { get; private set; } = "";
    public bool   PiperReady       => IsMale ? _piperReadyMale       : _piperReadyFemale;
    public bool   PiperDownloading => IsMale ? _piperDownloadingMale : _piperDownloadingFemale;

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

    /// <summary>"masculino" or "femenino". Controls voice selection across all engines.</summary>
    public string VoiceGender
    {
        get => _voiceGender;
        set => _voiceGender = value ?? "masculino";
    }

    /// <summary>When true, skip the low-quality Local WinRT fallback — prefer silence over bad voice.</summary>
    public bool SkipLocalFallback { get; set; }

    private bool IsMale => !string.Equals(_voiceGender, "femenino", StringComparison.OrdinalIgnoreCase);

    public SpeechEngine()
    {
        _piperReadyMale   = File.Exists(PiperExe) && File.Exists(VoiceModelMale);
        _piperReadyFemale = File.Exists(PiperExe) && File.Exists(VoiceModelFemale);
    }

    /// <summary>
    /// Pre-warm the Edge TTS pipeline by doing a real (tiny) synthesis.
    /// This warms DNS, TLS, and the server-side voice model so the first
    /// real Speak() gets audio from Edge instead of falling to Local WinRT.
    /// Call once after construction — fire-and-forget is fine.
    /// </summary>
    public async Task WarmUpAsync()
    {
        try
        {
            var voice = IsMale ? EdgeVoiceMalePrimary : EdgeVoiceFemalePrimary;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            // Synthesize a tiny phrase to truly warm the full pipeline
            await SynthesizeEdgeAsync(".", voice, cts.Token);
        }
        catch { /* best-effort warm-up */ }
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
        var male         = IsMale;
        var piperReady   = male ? _piperReadyMale       : _piperReadyFemale;
        var piperDling   = male ? _piperDownloadingMale : _piperDownloadingFemale;
        var piperModel    = male ? VoiceModelMale : VoiceModelFemale;
        // sharvard is multi-speaker: M=0, F=1. davefx is single-speaker (no --speaker arg).
        int? piperSpeaker = male ? null : 1;

        // Select Edge voices and prosody based on gender
        var edgePrimary  = male ? EdgeVoiceMalePrimary  : EdgeVoiceFemalePrimary;
        var edgeFallback = male ? EdgeVoiceMaleFallback : EdgeVoiceFemaleFallback;
        var edgePitch    = male ? "-3%" : "+0%";  // male: slightly lower; female: natural pitch
        var chatStyle    = male;                   // only AlvaroNeural supports express-as style='chat'

        // 1️⃣ Piper — offline neural
        if (piperReady)
        {
            try
            {
                audio = await SynthesizePiperAsync(text, piperModel, ct, piperSpeaker);
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
            if (!piperReady && !piperDling)
                _ = Task.Run(() => DownloadPiperCoreAsync(male), CancellationToken.None);

            try
            {
                audio = await SynthesizeEdgeAsync(text, edgePrimary, ct, edgePitch, chatStyle);
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
                    audio = await SynthesizeEdgeAsync(text, edgeFallback, ct, edgePitch, chatStyle);
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

        // 3️⃣ Local fallback (skipped when SkipLocalFallback is set)
        //    Male   → WinRT OneCore (better neural voices on Win11)
        //    Female → SAPI System.Speech (SelectVoiceByHints forces female reliably)
        if ((audio == null || audio.Length == 0) && !SkipLocalFallback)
        {
            if (!piperReady && !piperDling)
                _ = Task.Run(() => DownloadPiperCoreAsync(male), CancellationToken.None);

            try
            {
                audio = male
                    ? await SynthesizeLocalAsync(text, true, ct)
                    : await SynthesizeSapiAsync(text, ct);
                if (audio is { Length: > 0 })
                {
                    LastEngine = male ? "local-winrt" : "local-sapi";
                    EngineUsed?.Invoke(LastEngine);
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

    private static async Task<byte[]?> SynthesizePiperAsync(string text, string voiceModel, CancellationToken ct, int? speakerId = null)
    {
        var wavFile = Path.Combine(Path.GetTempPath(), $"ares_tts_{Guid.NewGuid():N}.wav");
        try
        {
            var speakerArg = speakerId.HasValue ? $" --speaker {speakerId.Value}" : "";
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName  = PiperExe,
                Arguments = $"--model \"{voiceModel}\" --output_file \"{wavFile}\" --length_scale 1.05{speakerArg}",
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

    /// <summary>Downloads Piper binary + voice models for both genders.</summary>
    public async Task DownloadPiperAsync()
    {
        await Task.WhenAll(
            DownloadPiperCoreAsync(true),
            DownloadPiperCoreAsync(false));
    }

    private async Task DownloadPiperCoreAsync(bool male)
    {
        if (male)
        {
            if (_piperReadyMale || _piperDownloadingMale) return;
            _piperDownloadingMale = true;
        }
        else
        {
            if (_piperReadyFemale || _piperDownloadingFemale) return;
            _piperDownloadingFemale = true;
        }

        var voiceModel    = male ? VoiceModelMale   : VoiceModelFemale;
        var voiceConfig   = male ? VoiceConfigMale  : VoiceConfigFemale;
        var voiceModelUrl = (male ? PiperVoiceMaleBaseUrl + PiperVoiceMaleName
                                  : PiperVoiceFemaleBaseUrl + PiperVoiceFemaleName) + ".onnx";
        var voiceConfigUrl = voiceModelUrl + ".json";

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

            if (!File.Exists(voiceModel))
            {
                using var resp = await http.GetAsync(voiceModelUrl, HttpCompletionOption.ResponseHeadersRead);
                resp.EnsureSuccessStatusCode();
                using var fs = File.Create(voiceModel);
                await resp.Content.CopyToAsync(fs);
            }

            if (!File.Exists(voiceConfig))
            {
                var bytes = await http.GetByteArrayAsync(voiceConfigUrl);
                await File.WriteAllBytesAsync(voiceConfig, bytes);
            }

            var ready = File.Exists(PiperExe) && File.Exists(voiceModel);
            if (male) _piperReadyMale   = ready;
            else      _piperReadyFemale = ready;
        }
        catch { /* retry on next speak */ }
        finally
        {
            if (male) _piperDownloadingMale   = false;
            else      _piperDownloadingFemale = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  2. Edge TTS — online neural via WebSocket
    // ═══════════════════════════════════════════════════════════════

    private static async Task<byte[]?> SynthesizeEdgeAsync(string text, string voice, CancellationToken ct, string pitch = "-3%", bool useChatStyle = true)
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

        var escaped = System.Security.SecurityElement.Escape(text);
        string ssml;
        if (useChatStyle)
        {
            // AlvaroNeural supports express-as style='chat' + prosody
            ssml =
                $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' " +
                $"xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='es-ES'>" +
                $"<voice name='{voice}'>" +
                $"<mstts:express-as style='chat'>" +
                $"<prosody rate='0.92' pitch='{pitch}'>{escaped}</prosody>" +
                $"</mstts:express-as></voice></speak>";
        }
        else
        {
            // DaliaNeural / ElviraNeural: support <prosody> but NOT <mstts:express-as>
            ssml =
                $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='es-ES'>" +
                $"<voice name='{voice}'>" +
                $"<prosody rate='0.92' pitch='{pitch}'>{escaped}</prosody>" +
                $"</voice></speak>";
        }

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

    private static async Task<byte[]?> SynthesizeLocalAsync(string text, bool male, CancellationToken ct)
    {
        using var synth = new SpeechSynthesizer();
        var voices = SpeechSynthesizer.AllVoices;
        var targetGender = male
            ? Windows.Media.SpeechSynthesis.VoiceGender.Male
            : Windows.Media.SpeechSynthesis.VoiceGender.Female;

        // Prefer newer neural voices (Windows 11) over legacy robotic voices
        var chosen = male
            ? voices.FirstOrDefault(v => v.DisplayName.Contains("Pablo", StringComparison.OrdinalIgnoreCase))
              ?? voices.FirstOrDefault(v => v.DisplayName.Contains("Alvaro", StringComparison.OrdinalIgnoreCase))
            : voices.FirstOrDefault(v => v.DisplayName.Contains("Elvira", StringComparison.OrdinalIgnoreCase));

        chosen ??= voices.FirstOrDefault(v =>
                v.Language.StartsWith("es-ES", StringComparison.OrdinalIgnoreCase)
                && v.Gender == targetGender)
            ?? voices.FirstOrDefault(v =>
                v.Language.StartsWith("es", StringComparison.OrdinalIgnoreCase)
                && v.Gender == targetGender)
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
    //  4. SAPI (System.Speech) — reliable female voice via gender hint
    // ═══════════════════════════════════════════════════════════════

    private static async Task<byte[]?> SynthesizeSapiAsync(string text, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var synth = new System.Speech.Synthesis.SpeechSynthesizer();

            // Try Spanish female first, then any female, then any voice
            try
            {
                synth.SelectVoiceByHints(
                    System.Speech.Synthesis.VoiceGender.Female,
                    System.Speech.Synthesis.VoiceAge.Adult, 0,
                    new System.Globalization.CultureInfo("es"));
            }
            catch
            {
                try { synth.SelectVoiceByHints(System.Speech.Synthesis.VoiceGender.Female); }
                catch { /* use system default */ }
            }

            var lang = synth.Voice?.Culture?.Name ?? "es-ES";
            var escaped = System.Security.SecurityElement.Escape(text);
            // Match the same prosody tweaks used on male WinRT: slower rate, natural pitch
            var ssml =
                $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{lang}'>" +
                $"<prosody rate='0.85' pitch='+0%'><break time='150ms'/>{escaped}</prosody>" +
                $"</speak>";

            using var ms = new MemoryStream();
            synth.SetOutputToWaveStream(ms);
            synth.SpeakSsml(ssml);
            return ms.ToArray();
        }, ct);
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

            // Prepend 300 ms of silence before the speech so the audio device has time
            // to fully initialize before the first syllable. Without this the OS audio
            // engine eats the first ~100-200 ms while it opens the device.
            var withSilence = new OffsetSampleProvider(reader.ToSampleProvider())
            {
                DelayBy = TimeSpan.FromMilliseconds(300)
            };

            player = new WaveOutEvent { DesiredLatency = 400, NumberOfBuffers = 4, Volume = _volume };
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

        // Remove JSON fragments  { ... }
        text = Regex.Replace(text, @"\{[^}]{2,}\}", "");

        // Remove Windows file paths  (C:\..., D:\...)
        text = Regex.Replace(text, @"[A-Z]:\\[\w\\.\-\s]+", "");

        // Remove Unix-style paths  (/usr/bin/...)
        text = Regex.Replace(text, @"(?<!\w)/(?:usr|home|etc|tmp|var|opt|bin|mnt|dev)[\w/.\-]*", "");

        // Remove tool names the LLM might echo (open_app, search_web, etc.)
        text = Regex.Replace(text, @"\b\w+_\w+(?:_\w+)*\b", "");

        // Remove .exe / .dll / .msi / .bat references
        text = Regex.Replace(text, @"\b\S+\.(?:exe|dll|msi|bat|cmd|ps1|sh)\b", "", RegexOptions.IgnoreCase);

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

        // Remove angle brackets and their content (HTML/XML tags)
        text = Regex.Replace(text, @"<[^>]+>", "");

        // Remove parenthetical technical content (paths, params inside parens)
        text = Regex.Replace(text, @"\([^)]*[\\/:.][^)]*\)", "");

        // Replace repeated punctuation (... --- ===)
        text = Regex.Replace(text, @"\.{2,}", ".");
        text = Regex.Replace(text, @"[-=]{2,}", " ");

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
