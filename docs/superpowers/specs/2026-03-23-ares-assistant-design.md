# ARES — Autonomous Response Engine System
## Design Specification
**Date:** 2026-03-23
**Platform:** C# (.NET 8) + WPF
**Status:** Approved

---

## 1. Overview

ARES is a local AI desktop assistant for Windows with a futuristic red-themed UI. It integrates deeply with the operating system, allowing the user to control their PC through natural language text conversation. It uses a local Ollama LLM (no internet required for AI inference) and an agent loop architecture where the AI calls typed tools to perform actions.

**Core identity:**
- Name: ARES (Autonomous Response Engine System)
- Logo: Diamond + crosshair with animated rings, red (#ff2222)
- Primary language: Spanish
- AI backend: Ollama (`qwen2.5:32b` recommended, configurable)

---

## 2. Architecture

Five layers:

```
┌─────────────────────────────────────────┐
│           UI Layer (WPF)                │
│  SplashWindow │ OverlayMode │ FullHudMode │ SettingsWindow │
├─────────────────────────────────────────┤
│           Agent Loop (AI)               │
│  OllamaClient → AgentLoop → ToolDispatcher │
├─────────────────────────────────────────┤
│           Tool Registry                 │
│  Auto-generated tools + Built-in tools  │
├─────────────────────────────────────────┤
│           Safety System                 │
│  PermissionManager + ActionLogger       │
├─────────────────────────────────────────┤
│           System Scanner                │
│  AppScanner · FolderScanner · BrowserScanner │
└─────────────────────────────────────────┘
```

### Key classes
| Class | Responsibility |
|---|---|
| `App.xaml.cs` | Entry point, first-launch detection |
| `SplashWindow` | Animated boot screen with scanning progress |
| `MainWindow` | Single WPF Window shell; swaps between `OverlayModeControl` and `FullHudModeControl` UserControls |
| `OverlayModeControl` | Compact UserControl shown in overlay mode |
| `FullHudModeControl` | Expanded UserControl shown in full HUD mode |
| `SettingsWindow` | Separate WPF Window for all customization options |
| `OllamaClient` | HTTP client for Ollama API (`localhost:11434`), handles streaming |
| `AgentLoop` | Sends messages, receives tool calls, dispatches them, enforces iteration cap |
| `ToolDispatcher` | Receives tool call name+args from AgentLoop, looks up tool in ToolRegistry, calls PermissionManager, executes |
| `ToolRegistry` | Dictionary of all registered `ITool` instances, keyed by name |
| `ConversationHistory` | In-memory list of `OllamaMessage`; optionally persisted to `data/chat-history.json`; exposes `Add()`, `Clear()`, `ToList()`, `ExportToTxt()` |
| `SystemScanner` | Orchestrates AppScanner + FolderScanner + BrowserScanner on first launch |
| `PermissionManager` | Returns `PermissionLevel` for a given tool name + path; enforces blocked-path rules |
| `ConfigManager` | Reads/writes `data/config.json`; exposes strongly-typed `AppConfig` record |
| `ThemeEngine` | Reads `AppConfig.AccentColor` and applies it to `Application.Current.Resources` as `AccentBrush`, `AccentGlowBrush`, etc. |
| `ActionLogger` | Appends timestamped entries to `data/logs/actions.log`; rotates at 10MB |
| `GlobalHotkeyManager` | Registers/unregisters global hotkeys via `RegisterHotKey` P/Invoke on `MainWindow`'s `HwndSource` |

---

## 3. Window Architecture

`MainWindow` is a **single WPF `Window`** with `WindowStyle=None`, `AllowsTransparency=True`, and `Background=Transparent`. It hosts two `UserControl` instances that are swapped via `Visibility`:

- `OverlayModeControl` — compact chat panel, ~380×600px
- `FullHudModeControl` — expanded HUD with sidebars, ~1200×800px

**Mode switching** is handled inside `MainWindow` by toggling visibility and animating size/position with a `DoubleAnimation` on `Width`, `Height`, and `Margin`. There are no separate `Window` instances for the two modes.

`SettingsWindow` is a **separate `Window`** opened with `ShowDialog()`.

`SplashWindow` is a **separate `Window`** shown only on first launch, closed when scanning completes.

---

## 4. First Launch — Boot Sequence

On first run, `data/tools.json` does not exist → show `SplashWindow`.

**SplashWindow flow:**
1. Animated ARES logo (diamond rotates via `RotateTransform` + `DoubleAnimation`, rings pulse with `ScaleTransform`)
2. Status `TextBlock` updates in real time:
   - "Inicializando ARES..."
   - "Escaneando aplicaciones instaladas..." (Windows Registry `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall` + Start Menu `.lnk` shortcuts)
   - "Detectando carpetas importantes..." (Desktop, Documents, Downloads, git repos `.git`, Visual Studio `.sln`)
   - "Buscando navegadores..." (Chrome, Edge, Firefox known paths)
   - "Verificando conexión con Ollama..."
   - "Generando base de herramientas..."
   - "ARES listo."
3. Results saved to `data/tools.json`
4. `SplashWindow` closes, `MainWindow` opens in overlay mode

On subsequent launches: load `data/tools.json` directly, show brief 1.5s splash then open `MainWindow`.

**Reading `.lnk` shortcuts:** Use COM interop via `WshShortcut`. Add COM reference: *Windows Script Host Object Model* (not a NuGet package — added via *Project > Add COM Reference* in Visual Studio).

**Manual rescan** available from Settings at any time; rewrites `data/tools.json`.

---

## 5. Ollama Integration

### OllamaClient

Sends requests to `http://localhost:11434/api/chat` using `System.Net.Http.HttpClient` (built-in, no NuGet needed). Streaming is supported via `ReadAsStreamAsync` + `StreamReader` so tokens appear progressively in the chat UI.

**Request format (tool calling):**
```json
{
  "model": "qwen2.5:32b",
  "stream": true,
  "messages": [
    { "role": "system", "content": "..." },
    { "role": "user", "content": "abre spotify" }
  ],
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "open_spotify",
        "description": "Abre la aplicación Spotify",
        "parameters": {
          "type": "object",
          "properties": {},
          "required": []
        }
      }
    }
  ]
}
```

**Response with tool call:**
```json
{
  "message": {
    "role": "assistant",
    "content": "",
    "tool_calls": [
      {
        "function": {
          "name": "open_spotify",
          "arguments": {}
        }
      }
    ]
  }
}
```

**Response with text:**
```json
{
  "message": {
    "role": "assistant",
    "content": "Spotify abierto correctamente."
  }
}
```

`OllamaClient` exposes:
```csharp
Task<OllamaResponse> ChatAsync(List<OllamaMessage> messages, List<ToolDefinition> tools);
Task<bool> IsAvailableAsync(); // GET /api/tags, checks if server responds
Task<List<string>> GetInstalledModelsAsync(); // GET /api/tags, returns model names
```

### ConversationHistory

In-memory `List<OllamaMessage>`. Each message: `Role` (system/user/assistant/tool), `Content` (string), optional `ToolCallId`.

```csharp
public class ConversationHistory {
    public void Add(OllamaMessage message);
    public void Clear();
    public List<OllamaMessage> ToList();        // returns copy
    public void ExportToTxt(string path);
    public void LoadFromJson(string path);      // on startup if persistence enabled
    public void SaveToJson(string path);        // called after each exchange if persistence enabled
}
```

---

## 6. Agent Loop

```csharp
// Max iterations guard — prevents infinite tool-call loops
private const int MaxIterations = 10;

public async Task RunAsync(string userMessage) {
    history.Add(new OllamaMessage("user", userMessage));
    int iterations = 0;

    while (iterations < MaxIterations) {
        iterations++;
        var response = await ollamaClient.ChatAsync(history.ToList(), toolRegistry.GetToolDefinitions());

        if (response.Message.ToolCalls?.Count > 0) {
            foreach (var call in response.Message.ToolCalls) {
                string result = await toolDispatcher.ExecuteAsync(call.Function.Name, call.Function.Arguments);
                history.Add(new OllamaMessage("tool", result));
            }
            // Loop back to get AI's final text response
        } else {
            history.Add(new OllamaMessage("assistant", response.Message.Content));
            OnResponseReceived(response.Message.Content);
            return;
        }
    }
    // Max iterations hit
    OnResponseReceived("ARES: He alcanzado el límite de acciones consecutivas. Por favor, reformula tu petición.");
}
```

**System prompt (base, injected as first message):**
```
Eres ARES, un asistente de IA integrado en el sistema operativo del usuario.
Respondes siempre en español. Eres directo, eficiente y ligeramente formal.
Tienes acceso a herramientas para controlar el ordenador del usuario.
Cuando una acción es rechazada por el usuario, no la reintentas y lo indicas claramente.
Nunca borres archivos ni mates procesos del sistema.
```

**Personality modifier** — appended dynamically to system prompt based on `AppConfig.Personality`:
- Formal: *(default, no addition)*
- Casual: `"Usa un tono informal y cercano."`
- Sarcástico: `"Puedes ser levemente sarcástico y con humor."`
- Técnico: `"Usa terminología técnica precisa en tus respuestas."`

**Response length modifier** — appended dynamically:
- Conciso: `"Responde siempre de forma muy breve, máximo 2 frases."`
- Normal: *(default)*
- Detallado: `"Explica tus acciones y razonamientos con detalle."`

---

## 7. Tool System

### ITool interface
```csharp
public interface ITool {
    string Name { get; }           // snake_case, matches function name sent to Ollama
    string Description { get; }   // shown to AI in tool definition
    ToolParameterSchema Parameters { get; } // JSON Schema object for the tool's arguments
    Task<ToolResult> ExecuteAsync(Dictionary<string, JsonElement> args);
}

public record ToolResult(bool Success, string Message); // Message shown to AI as tool output
```

### ToolRegistry
```csharp
public class ToolRegistry {
    public void Register(ITool tool);
    public ITool? Get(string name);
    public List<ToolDefinition> GetToolDefinitions(); // converts all tools to Ollama format
    public void LoadFromJson(string toolsJsonPath);   // loads auto-generated tools on startup
}
```

### ToolDispatcher
```csharp
public class ToolDispatcher {
    public async Task<string> ExecuteAsync(string toolName, Dictionary<string, JsonElement> args) {
        var tool = registry.Get(toolName);
        if (tool == null) return "Error: herramienta desconocida.";

        var permission = permissionManager.GetLevel(toolName, args);

        if (permission == PermissionLevel.Blocked)
            return "Acción bloqueada por el sistema de seguridad.";

        if (permission == PermissionLevel.Confirm) {
            bool approved = await ShowConfirmationDialogAsync(toolName, args);
            if (!approved) return "Acción cancelada por el usuario.";
        }

        actionLogger.Log(permission, toolName, args);
        var result = await tool.ExecuteAsync(args);
        return result.Message;
    }
}
```

### Auto-generated tools (from `tools.json`)
Format:
```json
{
  "open_chrome": { "type": "open_app", "display_name": "Google Chrome", "path": "C:/Program Files/Google/Chrome/Application/chrome.exe" },
  "open_spotify": { "type": "open_app", "display_name": "Spotify", "path": "C:/Users/user/AppData/Roaming/Spotify/Spotify.exe" },
  "open_documents": { "type": "open_folder", "display_name": "Documentos", "path": "C:/Users/user/Documents" },
  "search_chrome": { "type": "search_browser", "display_name": "Buscar en Chrome", "browser_path": "C:/Program Files/Google/Chrome/Application/chrome.exe" }
}
```

`ToolRegistry.LoadFromJson()` uses a factory: reads `type` field, instantiates `OpenAppTool`, `OpenFolderTool`, or `SearchBrowserTool` with the given path, then calls `Register()`.

### Built-in tools — full parameter schemas

| Tool | Permission | Parameters | Behavior |
|---|---|---|---|
| `close_app` | CONFIRM | `app_name: string` (window title or process name) | Finds process by `MainWindowTitle` contains match, calls `CloseMainWindow()` (graceful WM_CLOSE) |
| `take_screenshot` | AUTO | *(none)* | `Graphics.CopyFromScreen`, saves to temp PNG, returns file path string. **Note:** qwen2.5:32b is text-only — the path is returned as text for the user to view; vision analysis requires switching to a multimodal model (e.g. `llava:13b`) in Settings |
| `read_file` | AUTO | `path: string`, `max_lines: int = 200` | Reads up to `max_lines` lines of a text file; returns content as string |
| `write_file` | CONFIRM | `path: string`, `content: string` | Writes full content to file. Blocked if path is inside `data/` directory |
| `run_command` | CONFIRM | `command: string` | Runs via `cmd.exe /C`. Whitelist-validated (see Section 8 Safety) |
| `search_web` | AUTO | `query: string` | Opens `https://www.google.com/search?q={query}` in default browser |
| `clipboard_read` | AUTO | *(none)* | Returns `Clipboard.GetText()` |
| `clipboard_write` | CONFIRM | `text: string` | Calls `Clipboard.SetText()` |
| `set_volume` | AUTO | `level: int (0-100)`, `mute: bool = false` | Uses NAudio `AudioEndpointVolume` |
| `get_system_info` | AUTO | *(none)* | Returns JSON: `{ cpu_percent, ram_used_gb, ram_total_gb, disk_free_gb, uptime_hours, current_time }` via `PerformanceCounter` and `DriveInfo` |
| `list_open_windows` | AUTO | *(none)* | `Process.GetProcesses()` filtered to those with non-empty `MainWindowTitle` |
| `minimize_window` | AUTO | `title: string` | `ShowWindow(hwnd, SW_MINIMIZE)` P/Invoke on first match by title |
| `maximize_window` | AUTO | `title: string` | `ShowWindow(hwnd, SW_MAXIMIZE)` P/Invoke |
| `type_text` | CONFIRM | `text: string` | `SendKeys.Send()` via `System.Windows.Forms`; requires `PackageReference` for `Microsoft.Windows.Compatibility` |

---

## 8. Safety System

### Permission levels
| Level | Behavior |
|---|---|
| AUTO 🟢 | Executed immediately, logged |
| CONFIRM 🟡 | `ConfirmationDialog` shown (modal, red border, shows tool name + args). User clicks Approve or Cancel. If cancelled, agent loop receives "cancelled" message and does not retry. |
| BLOCKED 🔴 | Never executes. Returns blocked message to AI. |

### run_command whitelist
`RunCommandTool` validates command against a whitelist of allowed prefixes. Anything not on the list is blocked.

**Allowed:**
```
ipconfig, ping, tracert, nslookup, netstat, dir, echo, type,
tasklist, systeminfo, whoami, hostname, ver, date, time,
mkdir, cd, cls, tree, where, wmic (readonly queries only)
```

**Explicitly blocked patterns** (checked before whitelist):
```
del, rmdir, rd, format, reg add, reg delete, reg import,
net user, net localgroup, sc delete, sc stop, bcdedit,
shutdown, restart, powershell -enc, curl (external downloads),
wget, Invoke-Expression, iex
```

### Path-based blocking in PermissionManager
`write_file` and `run_command` check against blocked path prefixes:
```
C:\Windows\
C:\Program Files\
C:\Program Files (x86)\
{AppDataPath}\ARES\data\          ← ARES own config/data directory
{SYSTEMROOT}\System32\
```

### Agent loop safety
- Max 10 consecutive tool calls per user message (see AgentLoop above)
- If user cancels a CONFIRM action, the string `"Acción cancelada por el usuario."` is sent back to the AI as tool result — the AI is expected to acknowledge and stop
- All actions logged to `data/logs/actions.log` with format: `[2026-03-23 14:32:11] AUTO | open_chrome | {"result":"ok"}`
- Log rotates at 10MB

---

## 9. Global Hotkeys

Implemented via P/Invoke `RegisterHotKey` / `UnregisterHotKey` on the `MainWindow` HWND, captured via `HwndSource.AddHook`.

```csharp
public class GlobalHotkeyManager : IDisposable {
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public void Register(IntPtr hwnd, int id, ModifierKeys modifiers, Key key);
    public void Unregister(IntPtr hwnd, int id);
    public event EventHandler<int> HotkeyPressed; // fires with hotkey id
}
```

Default hotkeys (configurable in Settings):
- `Ctrl+Space` → show/hide overlay
- `Ctrl+Shift+Space` → toggle overlay / full HUD

---

## 10. Customization & Settings

All settings stored in `data/config.json` as a strongly-typed `AppConfig` record:

```csharp
public record AppConfig {
    public string AccentColor { get; init; } = "#ff2222";
    public double OverlayOpacity { get; init; } = 0.85;
    public string FontSize { get; init; } = "medium";       // small | medium | large
    public string OverlayPosition { get; init; } = "bottom-right"; // 4 corners
    public string OverlaySize { get; init; } = "normal";    // compact | normal | wide
    public string OllamaModel { get; init; } = "qwen2.5:32b";
    public string AssistantName { get; init; } = "ARES";
    public string Personality { get; init; } = "formal";   // formal | casual | sarcastico | tecnico
    public string ResponseLength { get; init; } = "normal"; // conciso | normal | detallado
    public string ShowHideHotkey { get; init; } = "Ctrl+Space";
    public string ToggleModeHotkey { get; init; } = "Ctrl+Shift+Space";
    public bool LaunchWithWindows { get; init; } = false;
    public bool SaveChatHistory { get; init; } = true;
    public string Language { get; init; } = "es";
}
```

`ThemeEngine.Apply(AppConfig config)` updates `Application.Current.Resources`:
- `AccentBrush` → `SolidColorBrush(AccentColor)`
- `AccentGlowBrush` → `AccentColor` at 40% opacity
- `FontSizeNormal` → 13/15/17 based on `FontSize` setting

---

## 11. MVVM Pattern

The app uses a lightweight MVVM pattern:
- Each Window/UserControl has a corresponding ViewModel in `ViewModels/`
- ViewModels implement `INotifyPropertyChanged`
- Data binding via standard WPF `{Binding}`
- No MVVM framework dependency — uses built-in WPF binding only

| ViewModel | Owned by |
|---|---|
| `MainViewModel` | `MainWindow` |
| `ChatViewModel` | `OverlayModeControl` + `FullHudModeControl` |
| `SettingsViewModel` | `SettingsWindow` |
| `SplashViewModel` | `SplashWindow` |

---

## 12. Project Structure

```
AresAssistant/
├── App.xaml / App.xaml.cs
├── Windows/
│   ├── SplashWindow.xaml / .cs
│   ├── MainWindow.xaml / .cs
│   └── SettingsWindow.xaml / .cs
├── Controls/
│   ├── OverlayModeControl.xaml / .cs
│   ├── FullHudModeControl.xaml / .cs
│   └── ConfirmationDialog.xaml / .cs
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── ChatViewModel.cs
│   ├── SettingsViewModel.cs
│   └── SplashViewModel.cs
├── Core/
│   ├── AgentLoop.cs
│   ├── OllamaClient.cs
│   ├── ConversationHistory.cs
│   └── ToolDispatcher.cs
├── Tools/
│   ├── ITool.cs
│   ├── ToolRegistry.cs
│   ├── OpenAppTool.cs
│   ├── OpenFolderTool.cs
│   ├── CloseAppTool.cs
│   ├── ScreenshotTool.cs
│   ├── ReadFileTool.cs
│   ├── WriteFileTool.cs
│   ├── RunCommandTool.cs
│   ├── SearchWebTool.cs
│   ├── ClipboardReadTool.cs
│   ├── ClipboardWriteTool.cs
│   ├── VolumeTool.cs
│   ├── SystemInfoTool.cs
│   ├── ListWindowsTool.cs
│   ├── MinimizeWindowTool.cs
│   ├── MaximizeWindowTool.cs
│   ├── TypeTextTool.cs
│   └── SearchBrowserTool.cs
├── Scanner/
│   ├── SystemScanner.cs
│   ├── AppScanner.cs
│   ├── FolderScanner.cs
│   └── BrowserScanner.cs
├── Safety/
│   ├── PermissionManager.cs
│   └── ActionLogger.cs
├── Config/
│   ├── ConfigManager.cs
│   ├── ThemeEngine.cs
│   └── AppConfig.cs
├── Helpers/
│   ├── GlobalHotkeyManager.cs   ← P/Invoke RegisterHotKey
│   ├── WindowNativeMethods.cs   ← P/Invoke ShowWindow, SetForegroundWindow
│   └── SendInputHelper.cs       ← P/Invoke SendInput (for type_text)
├── Models/
│   ├── OllamaMessage.cs
│   ├── OllamaResponse.cs
│   ├── ToolDefinition.cs
│   └── ToolResult.cs
├── Assets/
│   └── logo/                    ← ARES SVG logo assets
├── data/                        ← runtime data, gitignored
│   ├── tools.json
│   ├── config.json
│   ├── chat-history.json
│   └── logs/
│       └── actions.log
└── AresAssistant.csproj
```

---

## 13. NuGet Dependencies

| Package | Purpose | Note |
|---|---|---|
| `Newtonsoft.Json` | JSON serialization/deserialization | Chosen for flexibility with dynamic JSON; `System.Text.Json` can substitute |
| `NAudio` | System volume control via `AudioEndpointVolume` | |
| `Microsoft.Windows.Compatibility` | Provides `System.Windows.Forms` for `SendKeys` in `TypeTextTool` | Only needed for that one tool |
| `System.Drawing.Common` | `Graphics.CopyFromScreen` for screenshots | Windows-only; acceptable for this app |

**COM Reference (not NuGet):**
- *Windows Script Host Object Model* — added via *Project → Add COM Reference → Windows Script Host Object Model*. Provides `WshShortcut` for reading `.lnk` Start Menu shortcuts in `AppScanner`.

**Built-in (.NET 8, no install needed):**
- `System.Net.Http.HttpClient` — Ollama HTTP calls
- `Microsoft.Win32.Registry` — reading installed apps from registry
- P/Invoke for `RegisterHotKey`, `ShowWindow`, `SendInput`

---

## 14. Setup Guide (for user)

1. Install Ollama from **ollama.com**
2. Open terminal and run: `ollama pull qwen2.5:32b` (downloads ~20GB)
3. Verify: `ollama run qwen2.5:32b "hola"` — should respond
4. Build ARES in Visual Studio 2022+ (.NET 8 SDK required)
5. Run ARES — first launch triggers system scan automatically
6. In Settings, confirm the Ollama model name matches what you downloaded
7. Optionally enable "Iniciar con Windows" in Settings

**Voice support (future):** Will use Windows Speech Recognition API or Whisper model via Ollama. Selectable in Settings alongside text mode.
