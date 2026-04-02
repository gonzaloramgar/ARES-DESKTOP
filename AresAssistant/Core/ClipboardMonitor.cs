using System.Windows;
using System.Windows.Threading;

namespace AresAssistant.Core;

public sealed class ClipboardMonitor
{
    private readonly DispatcherTimer _timer;
    private string _lastText = string.Empty;

    public bool Enabled { get; set; } = true;

    public event Action<string, string>? ClipboardSmartHint;

    public ClipboardMonitor()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += OnTick;
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        if (!Enabled) return;

        try
        {
            if (!Clipboard.ContainsText()) return;
            var text = Clipboard.GetText()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return;
            if (text == _lastText) return;

            _lastText = text;
            var hint = BuildHint(text);
            ClipboardSmartHint?.Invoke(text, hint);
        }
        catch
        {
            // Clipboard can be temporarily locked by another process.
        }
    }

    private static string BuildHint(string text)
    {
        if (Uri.TryCreate(text, UriKind.Absolute, out _))
            return "Portapapeles: detecté una URL. Puedes pedirme resumir la página o abrirla.";

        if (text.Length > 220)
            return "Portapapeles: texto largo detectado. Puedes pedirme resumirlo o extraer puntos clave.";

        if (text.Contains("Exception", StringComparison.OrdinalIgnoreCase)
            || text.Contains("error", StringComparison.OrdinalIgnoreCase)
            || text.Contains("stack trace", StringComparison.OrdinalIgnoreCase))
            return "Portapapeles: parece un error técnico. Puedes pedirme explicarlo y proponer solución.";

        return "Portapapeles actualizado. Puedo traducir, resumir o buscar este contenido.";
    }
}
