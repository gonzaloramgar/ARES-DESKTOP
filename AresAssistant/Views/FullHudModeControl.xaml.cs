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
            return;
        }

        if (e.Key == Key.Up)
        {
            e.Handled = Vm.NavigateInputHistory(-1);
            return;
        }

        if (e.Key == Key.Down)
        {
            e.Handled = Vm.NavigateInputHistory(1);
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
            Filter = "Markdown|*.md|Texto|*.txt|HTML|*.html",
            FileName = $"ares_chat_{DateTime.Now:yyyyMMddHHmmss}.md"
        };
        if (dlg.ShowDialog() == true)
        {
            var ext = System.IO.Path.GetExtension(dlg.FileName).ToLowerInvariant();
            if (ext == ".html")
            {
                var body = string.Join("\n", Vm.Messages.Select(m =>
                    $"<article class=\"msg {(m.IsUser ? "user" : "assistant")}\"><header>{System.Net.WebUtility.HtmlEncode(m.Role.ToUpper())} · {m.Timestamp:HH:mm}</header><pre>{System.Net.WebUtility.HtmlEncode(m.Content)}</pre></article>"));
                var html = "<!doctype html><html><head><meta charset=\"utf-8\"><style>body{font-family:Segoe UI;background:#0b0d14;color:#edf2ff;margin:24px}.msg{border:1px solid #2a3146;border-radius:10px;padding:12px;margin-bottom:10px;background:#13192a}.msg.user{border-color:#5a2a2a;background:#251616}header{font-size:12px;color:#9fb0c8;margin-bottom:8px}pre{white-space:pre-wrap;word-break:break-word;margin:0;font-family:Consolas,monospace}</style></head><body>" + body + "</body></html>";
                System.IO.File.WriteAllText(dlg.FileName, html);
            }
            else if (ext == ".md")
            {
                var md = string.Join("\n\n", Vm.Messages.Select(m =>
                    $"## {(m.IsUser ? "TÚ" : "ARES")} ({m.Timestamp:HH:mm})\n\n{m.Content}"));
                System.IO.File.WriteAllText(dlg.FileName, md);
            }
            else
            {
                var lines = Vm.Messages.Select(m => $"[{m.Role.ToUpper()} {m.Timestamp:HH:mm}]: {m.Content}");
                System.IO.File.WriteAllLines(dlg.FileName, lines);
            }
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

    private void CancelResponse_Click(object sender, RoutedEventArgs e)
        => Vm.CancelResponse();

    private void AttachScreen_Click(object sender, RoutedEventArgs e)
        => Vm.ArmScreenContextNextMessage();
}
