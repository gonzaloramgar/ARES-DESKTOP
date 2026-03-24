using AresAssistant.Models;
using Newtonsoft.Json.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace AresAssistant.Tools;

public class ScreenshotTool : ITool
{
    public string Name => "take_screenshot";
    public string Description => "Toma una captura de pantalla y la guarda en un archivo temporal. Devuelve la ruta del archivo.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new(),
        Required = new()
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        try
        {
            var bounds = SystemInformation.VirtualScreen;
            using var bmp = new Bitmap(bounds.Width, bounds.Height);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

            var path = Path.Combine(Path.GetTempPath(), $"ares_screenshot_{DateTime.Now:yyyyMMddHHmmss}.png");
            bmp.Save(path, ImageFormat.Png);

            return Task.FromResult(new ToolResult(true, $"Captura guardada en: {path}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"Error al tomar captura: {ex.Message}"));
        }
    }
}
