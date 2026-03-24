using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Controls;

public partial class ConfirmationDialog : Window
{
    public ConfirmationDialog(string toolName, Dictionary<string, JToken> args)
    {
        InitializeComponent();

        ToolNameRun.Text = toolName;
        ArgsText.Text = args.Count > 0
            ? JsonConvert.SerializeObject(
                args.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString()),
                Formatting.Indented)
            : "(sin parámetros)";
    }

    private void Approve_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
