using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace AresAssistant.Views;

public partial class ColorPickerWindow : Window
{
    public string SelectedColor { get; private set; } = "#ff2222";

    private bool _updating;
    private bool _initialized;

    public ColorPickerWindow(string initialHex, Window owner)
    {
        InitializeComponent();
        Owner = owner;
        Loaded += (_, _) =>
        {
            _initialized = true;
            SetFromHex(initialHex);
        };
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => DragMove();

    // ─── Slider changed ──────────────────────────────────────────────────────
    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating || !_initialized) return;
        var color = HsvToColor(HueSlider.Value, SatSlider.Value / 100.0, ValSlider.Value / 100.0);
        ApplyColor(color, updateSliders: false);
    }

    // ─── Hex box changed ─────────────────────────────────────────────────────
    private void HexBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_updating || !_initialized) return;
        var text = HexBox.Text.TrimStart('#');
        if (text.Length == 6)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString("#" + text);
                ApplyColor(color, updateSliders: true);
            }
            catch { }
        }
    }

    // ─── Preset swatch ───────────────────────────────────────────────────────
    private void Preset_Click(object sender, RoutedEventArgs e)
        => SetFromHex((string)((FrameworkElement)sender).Tag);

    // ─── OK / Cancel ─────────────────────────────────────────────────────────
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        SelectedColor = "#" + HexBox.Text.TrimStart('#').ToUpper();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private void SetFromHex(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            ApplyColor(color, updateSliders: true);
        }
        catch { }
    }

    private void ApplyColor(Color color, bool updateSliders)
    {
        _updating = true;

        PreviewBorder.Background = new SolidColorBrush(color);
        var hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        PreviewHex.Text = hex;
        SelectedColor = hex;
        HexBox.Text = hex;

        // Update RGB channel labels
        RLabel.Text = color.R.ToString();
        GLabel.Text = color.G.ToString();
        BLabel.Text = color.B.ToString();

        // Update preview glow to match selected color
        PreviewGlow.Color = color;

        if (updateSliders)
        {
            var (h, s, v) = ColorToHsv(color);
            HueSlider.Value = h;
            SatSlider.Value = s * 100;
            ValSlider.Value = v * 100;
        }

        // Update S/V track gradients (depend on current H and V)
        var greyAtV  = HsvToColor(HueSlider.Value, 0, ValSlider.Value / 100.0);
        var satFull  = HsvToColor(HueSlider.Value, 1, ValSlider.Value / 100.0);
        SatTrackBg.Background = new LinearGradientBrush(greyAtV, satFull,
            new System.Windows.Point(0, 0), new System.Windows.Point(1, 0));

        var black  = HsvToColor(HueSlider.Value, SatSlider.Value / 100.0, 0);
        var bright = HsvToColor(HueSlider.Value, SatSlider.Value / 100.0, 1);
        ValTrackBg.Background = new LinearGradientBrush(black, bright,
            new System.Windows.Point(0, 0), new System.Windows.Point(1, 0));

        _updating = false;
    }

    // ─── HSV ↔ RGB conversion ─────────────────────────────────────────────────
    private static Color HsvToColor(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        int i = (int)(h / 60) % 6;
        double f = h / 60 - Math.Floor(h / 60);
        double p = v * (1 - s);
        double q = v * (1 - f * s);
        double t = v * (1 - (1 - f) * s);

        var (r, g, b) = i switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q)
        };

        return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static (double h, double s, double v) ColorToHsv(Color color)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double h = 0;
        if (delta > 0)
        {
            if (max == r)      h = 60 * (((g - b) / delta) % 6);
            else if (max == g) h = 60 * ((b - r) / delta + 2);
            else               h = 60 * ((r - g) / delta + 4);
        }
        if (h < 0) h += 360;

        double s = max == 0 ? 0 : delta / max;
        return (h, s, max);
    }
}

