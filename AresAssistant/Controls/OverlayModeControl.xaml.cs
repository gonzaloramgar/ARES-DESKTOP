using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AresAssistant.ViewModels;
using AresAssistant.Windows;

namespace AresAssistant.Controls;

public partial class OverlayModeControl : UserControl
{
    private ChatViewModel Vm => (ChatViewModel)DataContext;

    public OverlayModeControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ChatViewModel vm)
                vm.Messages.CollectionChanged += (_, _) => ScrollToBottom();
        };
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
        => await Vm.SendMessageAsync();

    private async void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            await Vm.SendMessageAsync();
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Window.GetWindow(this)?.DragMove();
    }

    private void ToggleMode_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mw)
            mw.ToggleMode();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var sw = new SettingsWindow { Owner = Window.GetWindow(this) };
        sw.ShowDialog();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        var win = Window.GetWindow(this);
        if (win == null) return;
        if (App.ConfigManager.Config.CloseToTray)
            win.Hide();
        else
            win.Close();
    }

    private void ScrollToBottom()
    {
        Dispatcher.BeginInvoke(() => ChatScroll.ScrollToEnd());
    }
}
