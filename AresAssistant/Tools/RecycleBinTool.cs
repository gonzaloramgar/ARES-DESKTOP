using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

/// <summary>
/// Lists, restores one item, or restores all items from the Windows Recycle Bin
/// using Shell32 COM (no extra packages needed).
/// </summary>
public class RecycleBinTool : ITool
{
    public string Name => "recycle_bin";
    public string Description =>
        "Gestiona la papelera de reciclaje de Windows. " +
        "Puede listar su contenido, recuperar un elemento específico por nombre, o restaurar todo de una vez.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["action"] = new()
            {
                Type = "string",
                Description = "'list' para ver el contenido, 'restore' para recuperar un elemento por nombre, 'restore_all' para recuperar todo."
            },
            ["name"] = new()
            {
                Type = "string",
                Description = "Nombre (o parte del nombre) del archivo o carpeta a recuperar. Solo para action='restore'."
            }
        },
        Required = new() { "action" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var action = args.TryGetValue("action", out var a) ? a.ToString().Trim().ToLower() : "";
        var name   = args.TryGetValue("name",   out var n) ? n.ToString().Trim() : "";

        return action switch
        {
            "list"        => Task.FromResult(ListBin()),
            "restore_all" => Task.FromResult(RestoreAll()),
            "restore"     => Task.FromResult(RestoreByName(name)),
            _             => Task.FromResult(new ToolResult(false,
                "Acción no válida. Usa 'list', 'restore' (con name=...) o 'restore_all'."))
        };
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static dynamic GetShellBinItems()
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application")
            ?? throw new InvalidOperationException("Shell.Application no disponible en este sistema.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        return shell.NameSpace(10).Items(); // 10 = CSIDL_BITBUCKET (Recycle Bin)
    }

    private static ToolResult ListBin()
    {
        try
        {
            var items = GetShellBinItems();
            int count = items.Count;
            if (count == 0) return new ToolResult(true, "La papelera está vacía.");

            var names = new List<string>();
            for (int i = 0; i < count; i++)
                names.Add((string)items.Item(i).Name);

            return new ToolResult(true,
                $"Papelera ({count} elemento{(count == 1 ? "" : "s")}):\n" +
                string.Join("\n", names.Select(nm => $"  • {nm}")));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Error al leer la papelera: {ex.Message}");
        }
    }

    private static ToolResult RestoreAll()
    {
        try
        {
            var items = GetShellBinItems();
            int count = items.Count;
            if (count == 0) return new ToolResult(true, "La papelera ya estaba vacía.");

            // Collect references before restoring (restoring removes items from the collection)
            var toRestore = new List<dynamic>();
            for (int i = 0; i < count; i++)
                toRestore.Add(items.Item(i));

            foreach (var item in toRestore)
                item.InvokeVerb("restore");

            return new ToolResult(true, $"Restaurados {count} elemento{(count == 1 ? "" : "s")} de la papelera.");
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Error al restaurar la papelera: {ex.Message}");
        }
    }

    private static ToolResult RestoreByName(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new ToolResult(false, "Especifica el nombre del elemento a recuperar (name=...).");

        try
        {
            var items = GetShellBinItems();
            int count = items.Count;

            var matches = new List<(string Name, dynamic Item)>();
            for (int i = 0; i < count; i++)
            {
                dynamic item = items.Item(i);
                string itemName = (string)item.Name;
                if (itemName.Contains(query, StringComparison.OrdinalIgnoreCase))
                    matches.Add((itemName, item));
            }

            if (matches.Count == 0)
                return new ToolResult(false, $"No se encontró '{query}' en la papelera.");

            foreach (var (_, item) in matches)
                item.InvokeVerb("restore");

            var restored = string.Join(", ", matches.Select(m => $"'{m.Name}'"));
            return new ToolResult(true, $"Restaurado: {restored}");
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Error al restaurar '{query}': {ex.Message}");
        }
    }
}
