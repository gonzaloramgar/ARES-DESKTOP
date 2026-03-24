using System.Windows;
using System.Windows.Input;
using AresAssistant.Core;
using AresAssistant.ViewModels;

namespace AresAssistant.Windows;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow()
    {
        InitializeComponent();
        _vm = new SettingsViewModel(App.ConfigManager, new OllamaClient());
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.CheckOllamaAsync();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => DragMove();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _vm.Save();
        Close();
    }

    private async void CheckOllama_Click(object sender, RoutedEventArgs e)
        => await _vm.CheckOllamaAsync();

    private async void Rescan_Click(object sender, RoutedEventArgs e)
    {
        var scanner = new Scanner.SystemScanner();
        scanner.StatusChanged += msg =>
            Dispatcher.Invoke(() => Title = $"ARES — {msg}");

        var tools = await scanner.ScanAsync();
        Scanner.SystemScanner.SaveToJson(tools, "data/tools.json");

        // Reload tools into current registry
        MainWindow.ToolRegistry.LoadFromJson("data/tools.json");

        Title = "ARES — Ajustes";
        MessageBox.Show("Escaneo completado. Las herramientas han sido actualizadas.", "ARES");
    }
}
