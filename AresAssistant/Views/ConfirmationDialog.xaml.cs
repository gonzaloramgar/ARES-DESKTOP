using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Views;

public partial class ConfirmationDialog : Window
{
    public ConfirmationDialog(string toolName, Dictionary<string, JToken> args)
    {
        InitializeComponent();

        QuestionText.Text = BuildMessage(toolName, args);
        ToolNameRun.Text = toolName;
        ArgsText.Text = args.Count > 0
            ? JsonConvert.SerializeObject(
                args.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString()),
                Formatting.Indented)
            : "(sin parámetros)";
    }

    private static string BuildMessage(string toolName, Dictionary<string, JToken> args)
    {
        string Get(string key) => args.TryGetValue(key, out var v) ? v.ToString() : "";
        string FriendlyPath(string p) =>
            Path.GetFileName(p.TrimEnd(Path.DirectorySeparatorChar, '/')) is { Length: > 0 } name ? name : p;

        return toolName switch
        {
            "delete_folder"   => $"¿Seguro que quieres eliminar la carpeta '{FriendlyPath(Get("path"))}'? Esta acción NO se puede deshacer.",
            "write_file"      => $"¿Seguro que quieres escribir en '{FriendlyPath(Get("path"))}'?",
            "run_command"     => $"¿Seguro que quieres ejecutar el comando:\n\"{Get("command")}\"?",
            "close_app"       => $"¿Seguro que quieres cerrar '{Get("name")}'?",
            "clipboard_write" => "¿Seguro que quieres sobreescribir el portapapeles?",
            "type_text"       => "¿Seguro que quieres escribir texto automáticamente en la aplicación activa?",
            _                 => $"¿Seguro que quieres ejecutar la acción '{toolName}'?"
        };
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
