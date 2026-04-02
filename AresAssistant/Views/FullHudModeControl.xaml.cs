using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AresAssistant.Helpers;
using AresAssistant.ViewModels;
using Microsoft.Win32;

namespace AresAssistant.Views;

public partial class FullHudModeControl : UserControl
{
    private ChatViewModel Vm => (ChatViewModel)DataContext;

    public FullHudModeControl()
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

    private void ClearChat_Click(object sender, RoutedEventArgs e)
    {
        if (AresMessageBox.Show("¿Deseas limpiar el historial del chat?", "ARES",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            Vm.ClearHistory();
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Texto|*.txt",
            FileName = $"ares_chat_{DateTime.Now:yyyyMMddHHmmss}.txt"
        };
        if (dlg.ShowDialog() == true)
        {
            var lines = Vm.Messages.Select(m => $"[{m.Role.ToUpper()}]: {m.Content}");
            System.IO.File.WriteAllLines(dlg.FileName, lines);
            AresMessageBox.Show("Chat exportado correctamente.", "ARES");
        }
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

    private void MessageBubble_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
            AnimationHelper.FadeSlideIn(el, fromY: AnimationHelper.SlideDistanceSmall);
    }

    private void CopyMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is ChatMessage msg)
            Clipboard.SetText(msg.Content);
    }

    private void SpeakMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is ChatMessage msg)
            Vm.SpeakText(msg.Content);
    }

    private void DeleteMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is ChatMessage msg)
            Vm.RemoveMessage(msg);
    }

    private async void RefreshProductivitySummary_Click(object sender, RoutedEventArgs e)
    {
        await Vm.RefreshProductivitySummaryAsync();
    }
}
