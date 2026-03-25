using AresAssistant.Core;
using Newtonsoft.Json.Linq;
using System.Security.Principal;
using System.Text;

namespace AresAssistant.Tools;

/// <summary>
/// Lists, restores one item, or restores all items from the Windows Recycle Bin
/// by directly reading $Recycle.Bin metadata files ($I* / $R*).
/// Does not use Shell32 COM, so it works reliably from any thread.
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

    // ── data model ───────────────────────────────────────────────────────────

    private sealed record RecycleItem(string MetaFile, string DataFile, string OriginalPath)
    {
        public string Name => Path.GetFileName(OriginalPath);
    }

    // ── discovery ────────────────────────────────────────────────────────────

    /// <summary>
    /// Enumerates all items in the current user's Recycle Bin across all fixed drives
    /// by reading $I metadata files in $Recycle.Bin\{SID}\.
    /// </summary>
    private static List<RecycleItem> GetRecycleItems()
    {
        var sid  = WindowsIdentity.GetCurrent().User?.Value ?? "";
        var list = new List<RecycleItem>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;

            var binPath = Path.Combine(drive.Name, "$Recycle.Bin", sid);
            if (!Directory.Exists(binPath)) continue;

            foreach (var metaFile in Directory.GetFiles(binPath, "$I*"))
            {
                var originalPath = ReadOriginalPath(metaFile);
                if (originalPath is null) continue;

                // $IXXXXXX.ext  →  $RXXXXXX.ext
                var dataFile = Path.Combine(
                    Path.GetDirectoryName(metaFile)!,
                    "$R" + Path.GetFileName(metaFile)[2..]);

                list.Add(new RecycleItem(metaFile, dataFile, originalPath));
            }
        }

        return list;
    }

    /// <summary>
    /// Parses a $I metadata file and returns the original path.
    ///
    /// Layout (Windows Vista+):
    ///   [0..7]   version (int64) — 1 = Vista/7, 2 = Win8+
    ///   [8..15]  original size  (int64)
    ///   [16..23] deletion time  (FILETIME)
    ///   Version 1: [24..543]  path, null-terminated UTF-16, 260 wchars fixed
    ///   Version 2: [24..27]   char count (int32); [28..] path UTF-16
    /// </summary>
    private static string? ReadOriginalPath(string metaFile)
    {
        try
        {
            var bytes = File.ReadAllBytes(metaFile);
            if (bytes.Length < 28) return null;

            var version = BitConverter.ToInt64(bytes, 0);

            if (version == 1)
            {
                if (bytes.Length < 26) return null;
                return Encoding.Unicode
                    .GetString(bytes, 24, Math.Min(520, bytes.Length - 24))
                    .TrimEnd('\0');
            }

            if (version == 2)
            {
                var charCount = BitConverter.ToInt32(bytes, 24);
                var byteCount = charCount * 2;
                if (bytes.Length < 28 + byteCount) return null;
                return Encoding.Unicode
                    .GetString(bytes, 28, byteCount)
                    .TrimEnd('\0');
            }

            return null;
        }
        catch { return null; }
    }

    // ── operations ───────────────────────────────────────────────────────────

    private static ToolResult ListBin()
    {
        try
        {
            var items = GetRecycleItems();
            if (items.Count == 0) return new ToolResult(true, "La papelera está vacía.");

            return new ToolResult(true,
                $"Papelera ({items.Count} elemento{(items.Count == 1 ? "" : "s")}):\n" +
                string.Join("\n", items.Select(i => $"  • {i.Name}  ← {i.OriginalPath}")));
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
            var items = GetRecycleItems();
            if (items.Count == 0) return new ToolResult(true, "La papelera ya estaba vacía.");

            var errors = new List<string>();
            var ok     = 0;

            foreach (var item in items)
            {
                var r = RestoreItem(item);
                if (r.Success) ok++;
                else errors.Add($"  {item.Name}: {r.Message}");
            }

            var msg = $"Restaurados {ok} de {items.Count} elemento{(items.Count == 1 ? "" : "s")}.";
            if (errors.Count > 0) msg += "\nErrores:\n" + string.Join("\n", errors);
            return new ToolResult(errors.Count == 0, msg);
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
            var matches = GetRecycleItems()
                .Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || i.OriginalPath.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
                return new ToolResult(false, $"No se encontró '{query}' en la papelera.");

            var errors = new List<string>();
            var ok     = 0;

            foreach (var item in matches)
            {
                var r = RestoreItem(item);
                if (r.Success) ok++;
                else errors.Add($"  {item.Name}: {r.Message}");
            }

            var names = string.Join(", ", matches.Select(m => $"'{m.Name}'"));
            var msg   = $"Restaurado{(ok == 1 ? "" : "s")}: {names}";
            if (errors.Count > 0) msg += "\nErrores:\n" + string.Join("\n", errors);
            return new ToolResult(errors.Count == 0, msg);
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Error al restaurar '{query}': {ex.Message}");
        }
    }

    /// <summary>Moves the $R data file/folder back to its original path and deletes the $I metadata.</summary>
    private static ToolResult RestoreItem(RecycleItem item)
    {
        try
        {
            // Recreate original parent directory if it no longer exists
            var originalDir = Path.GetDirectoryName(item.OriginalPath);
            if (!string.IsNullOrEmpty(originalDir) && !Directory.Exists(originalDir))
                Directory.CreateDirectory(originalDir);

            if (File.Exists(item.DataFile))
            {
                if (File.Exists(item.OriginalPath))
                    return new ToolResult(false,
                        $"Ya existe un archivo en la ruta de destino: {item.OriginalPath}");
                File.Move(item.DataFile, item.OriginalPath);
            }
            else if (Directory.Exists(item.DataFile))
            {
                if (Directory.Exists(item.OriginalPath))
                    return new ToolResult(false,
                        $"Ya existe una carpeta en la ruta de destino: {item.OriginalPath}");
                Directory.Move(item.DataFile, item.OriginalPath);
            }
            else
            {
                return new ToolResult(false,
                    $"No se encontró el archivo de datos en la papelera ({item.DataFile}). " +
                    "Es posible que ya haya sido restaurado manualmente.");
            }

            try { File.Delete(item.MetaFile); } catch { /* metadata deletion is non-critical */ }

            return new ToolResult(true, $"Restaurado: '{item.Name}' → {item.OriginalPath}");
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Error restaurando '{item.Name}': {ex.Message}");
        }
    }
}
