using System.Windows;
using System.Windows.Media;

namespace AresAssistant.Config;

public static class ThemeEngine
{
    public static void Apply(AppConfig config)
    {
        var resources = Application.Current.Resources;

        if (TryParseColor(config.AccentColor, out var accent))
        {
            resources["AccentBrush"] = new SolidColorBrush(accent);
            var glow = accent;
            glow.A = 102; // 40% opacity
            resources["AccentGlowBrush"] = new SolidColorBrush(glow);
        }

        resources["FontSizeNormal"] = config.FontSize switch
        {
            "small" => 13.0,
            "large" => 17.0,
            _ => 15.0
        };

        // Overlay window background — driven by the OverlayOpacity setting
        var alpha = (byte)(Math.Clamp(config.OverlayOpacity, 0.0, 1.0) * 255);
        resources["OverlayBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(alpha, 0x0d, 0x0d, 0x0d));
    }

    public static bool TryParseColor(string hex, out Color color)
    {
        color = Colors.Red;
        try
        {
            color = (Color)ColorConverter.ConvertFromString(hex);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
