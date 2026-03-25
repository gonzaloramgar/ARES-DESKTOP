using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AresAssistant.Config;

namespace AresAssistant.Views;

public partial class SetupWindow : Window
{
    private string _selectedColor = "#ff2222";
    private string _selectedPersonality = "formal";

    private Border[] _personalityCards = Array.Empty<Border>();

    public SetupWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _personalityCards = [CardFormal, CardCasual, CardSarcastico, CardTecnico];
            UpdateColorPreview(_selectedColor);
            HighlightPersonalityCard(_selectedPersonality);
        };
    }

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        var hex = (string)((FrameworkElement)sender).Tag;
        _selectedColor = hex;
        UpdateColorPreview(hex);

        // Live-update the theme so the whole window adapts
        App.ConfigManager.Update(c => c with { AccentColor = hex });
        ThemeEngine.Apply(App.ConfigManager.Config);
    }

    private void UpdateColorPreview(string hex)
    {
        if (ThemeEngine.TryParseColor(hex, out var color))
        {
            ColorPreview.Background = new SolidColorBrush(color);
            ColorHexText.Text = hex;
        }
    }

    private void Personality_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border card && card.Tag is string personality)
        {
            _selectedPersonality = personality;
            HighlightPersonalityCard(personality);
        }
    }

    private void HighlightPersonalityCard(string personality)
    {
        var accent = (SolidColorBrush)FindResource("AccentBrush");
        var unselected = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e));

        foreach (var card in _personalityCards)
        {
            bool isCurrent = (string)card.Tag == personality;
            card.BorderBrush = isCurrent ? accent : unselected;
            card.Background = isCurrent
                ? new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x16))
                : new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11));
        }
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = "ARES";

        App.ConfigManager.Update(c => c with
        {
            AccentColor = _selectedColor,
            Personality = _selectedPersonality,
            AssistantName = name,
            SetupCompleted = true
        });
        ThemeEngine.Apply(App.ConfigManager.Config);

        var splash = new SplashWindow(isFirstLaunch: true);
        splash.Show();
        Close();
    }
}
