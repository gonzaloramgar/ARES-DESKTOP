namespace AresAssistant.Tools;

/// <summary>
/// Resolves path aliases (Desktop, Documents, Downloads, etc.) to absolute paths.
/// </summary>
public static class PathResolver
{
    private static readonly Dictionary<string, string> KnownAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Desktop"]   = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        ["Documents"] = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        ["Downloads"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
        ["Pictures"]  = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        ["Music"]     = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        ["Videos"]    = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
    };

    public static string Resolve(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        // Normalize slashes
        input = input.Replace('/', Path.DirectorySeparatorChar);

        var parts = input.Split(Path.DirectorySeparatorChar, 2);
        if (KnownAliases.TryGetValue(parts[0], out var basePath))
            return parts.Length > 1 ? Path.Combine(basePath, parts[1]) : basePath;

        return input;
    }
}
