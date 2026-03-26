using System.Windows;
using AresAssistant.Core;
using AresAssistant.ViewModels;

namespace AresAssistant.Views;

public partial class SplashWindow : Window
{
    private readonly bool _isFirstLaunch;
    private readonly SplashViewModel _vm = new();

    public SplashWindow(bool isFirstLaunch)
    {
        InitializeComponent();
        DataContext = _vm;
        _isFirstLaunch = isFirstLaunch;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Always re-scan so new apps / Steam games / desktop shortcuts are detected
            await RunFirstLaunchScanAsync();

            OpenMainWindow();
        }
        catch (Exception ex)
        {
            App.WriteCrash("SplashWindow.OnLoaded", ex);
            UpdateStatus($"Error: {ex.Message}", 0);
            await Task.Delay(3000);
            try { OpenMainWindow(); }
            catch (Exception ex2)
            {
                App.WriteCrash("OpenMainWindow", ex2);
                System.Windows.MessageBox.Show(
                    $"Error crítico al iniciar ARES:\n\n{ex2.Message}\n\nRevisa los archivos crash_*.log en la carpeta data/",
                    "ARES - Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                Application.Current.Shutdown(1);
            }
        }
    }

    private async Task RunFirstLaunchScanAsync()
    {
        var scanner = new SystemScanner();
        int step = 0;
        var steps = new[] { 10, 30, 50, 65, 75, 90, 100 };

        scanner.StatusChanged += msg =>
        {
            Dispatcher.Invoke(() =>
            {
                _vm.StatusText = msg;
                if (step < steps.Length)
                    _vm.Progress = steps[step++];
            });
        };

        var tools = await scanner.ScanAsync();
        SystemScanner.SaveToJson(tools, "data/tools.json");

        UpdateStatus("ARES listo.", 100);
        await Task.Delay(600);
    }

    private void UpdateStatus(string text, int progress)
    {
        Dispatcher.Invoke(() =>
        {
            _vm.StatusText = text;
            _vm.Progress = progress;
        });
    }

    private void OpenMainWindow()
    {
        var mainWindow = new MainWindow();
        mainWindow.Show();
        Close();
    }
}
