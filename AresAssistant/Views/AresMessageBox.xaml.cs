using System.Windows;
using System.Windows.Input;

namespace AresAssistant.Views;

/// <summary>
/// Reemplazo personalizado de MessageBox.Show con el estilo visual de ARES.
/// Soporta los modos OK, OKCancel, YesNo y YesNoCancel.
/// </summary>
public partial class AresMessageBox : Window
{
    public enum RoutingDiagnosticAction
    {
        Close,
        Copy,
        InstallMissing,
        RepairAll
    }

    public MessageBoxResult Result { get; private set; } = MessageBoxResult.OK;

    private AresMessageBox()
    {
        InitializeComponent();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    // ═══════════════════════════════════════════════════════════════
    //  Public static API — drop-in replacement for MessageBox.Show
    // ═══════════════════════════════════════════════════════════════

    public static MessageBoxResult Show(string message, string title = "ARES",
        MessageBoxButton buttons = MessageBoxButton.OK)
    {
        var owner = GetActiveWindow();
        var dlg = new AresMessageBox();
        dlg.TitleText.Text = title.Replace("ARES — ", "").Replace("ARES - ", "").Trim();
        if (string.IsNullOrWhiteSpace(dlg.TitleText.Text)) dlg.TitleText.Text = "ARES";
        dlg.MessageText.Text = message;
        dlg.Owner = owner;
        dlg.BuildButtons(buttons);
        dlg.ShowDialog();
        return dlg.Result;
    }

    public static bool ShowInstallMissingPrompt(string message, string title = "ARES")
    {
        var owner = GetActiveWindow();
        var dlg = new AresMessageBox();
        dlg.TitleText.Text = title.Replace("ARES — ", "").Replace("ARES - ", "").Trim();
        if (string.IsNullOrWhiteSpace(dlg.TitleText.Text)) dlg.TitleText.Text = "ARES";
        dlg.MessageText.Text = message;
        dlg.Owner = owner;

        dlg.ButtonPanel.Children.Clear();
        dlg.AddButton("Cerrar", "GhostButton", MessageBoxResult.No);
        dlg.AddButton("Instalar faltantes", "AresButton", MessageBoxResult.Yes, primary: true);

        dlg.ShowDialog();
        return dlg.Result == MessageBoxResult.Yes;
    }

    public static RoutingDiagnosticAction ShowRoutingDiagnosticPrompt(
        string message,
        bool canInstallMissing,
        bool canRepairAll,
        string title = "ARES")
    {
        var owner = GetActiveWindow();
        var dlg = new AresMessageBox();
        dlg.TitleText.Text = title.Replace("ARES — ", "").Replace("ARES - ", "").Trim();
        if (string.IsNullOrWhiteSpace(dlg.TitleText.Text)) dlg.TitleText.Text = "ARES";
        dlg.MessageText.Text = message;
        dlg.Owner = owner;

        dlg.ButtonPanel.Children.Clear();
        dlg.AddButton("Cerrar", "GhostButton", MessageBoxResult.Cancel);
        dlg.AddButton("Copiar diagnóstico", "GhostButton", MessageBoxResult.No);

        if (canRepairAll)
            dlg.AddButton("Reparar todo IA", "AresButton", MessageBoxResult.Yes, primary: true);
        else if (canInstallMissing)
            dlg.AddButton("Instalar faltantes", "AresButton", MessageBoxResult.Yes, primary: true);

        dlg.ShowDialog();

        if (dlg.Result == MessageBoxResult.No)
            return RoutingDiagnosticAction.Copy;

        if (dlg.Result == MessageBoxResult.Yes)
            return canRepairAll ? RoutingDiagnosticAction.RepairAll : RoutingDiagnosticAction.InstallMissing;

        return RoutingDiagnosticAction.Close;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Button builder
    // ═══════════════════════════════════════════════════════════════

    private void BuildButtons(MessageBoxButton buttons)
    {
        ButtonPanel.Children.Clear();

        switch (buttons)
        {
            case MessageBoxButton.OKCancel:
                AddButton("Cancelar", "GhostButton", MessageBoxResult.Cancel);
                AddButton("Aceptar", "AresButton", MessageBoxResult.OK, primary: true);
                break;

            case MessageBoxButton.YesNo:
                AddButton("No", "GhostButton", MessageBoxResult.No);
                AddButton("Sí", "AresButton", MessageBoxResult.Yes, primary: true);
                break;

            case MessageBoxButton.YesNoCancel:
                AddButton("Cancelar", "GhostButton", MessageBoxResult.Cancel);
                AddButton("No", "GhostButton", MessageBoxResult.No);
                AddButton("Sí", "AresButton", MessageBoxResult.Yes, primary: true);
                break;

            default: // OK
                AddButton("Aceptar", "AresButton", MessageBoxResult.OK, primary: true);
                break;
        }
    }

    private void AddButton(string content, string styleKey, MessageBoxResult result, bool primary = false)
    {
        var btn = new System.Windows.Controls.Button
        {
            Content = content,
            Style = (Style)FindResource(styleKey),
            MinWidth = 90,
            Height = 34,
            Margin = new Thickness(primary ? 8 : 0, 0, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 12,
            FontWeight = FontWeights.Bold
        };
        btn.Click += (_, _) =>
        {
            Result = result;
            DialogResult = result == MessageBoxResult.Yes || result == MessageBoxResult.OK;
            Close();
        };
        ButtonPanel.Children.Add(btn);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════

    private static Window? GetActiveWindow()
    {
        foreach (Window w in Application.Current.Windows)
        {
            if (w.IsActive) return w;
        }
        return Application.Current.MainWindow;
    }
}
