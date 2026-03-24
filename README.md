# ARES — Autonomous Response Engine System

ARES es un asistente de IA para Windows construido en WPF y .NET 8, que corre sobre modelos de lenguaje locales a través de [Ollama](https://ollama.ai). Diseñado para operar como HUD flotante sobre el escritorio, permite controlar el sistema con lenguaje natural sin salir de lo que estés haciendo.

## Características

### Dos modos de interfaz
- **Modo Overlay** (380×600) — Panel compacto anclado a una esquina de la pantalla
- **Modo HUD Completo** (1200×800) — Interfaz completa con lista de herramientas, chat y estado del sistema

### Herramientas integradas
| Herramienta | Descripción |
|---|---|
| `open_app` | Abrir aplicaciones instaladas |
| `close_app` | Cerrar una aplicación por nombre |
| `search_browser` / `search_web` | Buscar en el navegador predeterminado |
| `read_file` / `write_file` | Leer y escribir archivos |
| `run_command` | Ejecutar comandos de sistema |
| `screenshot` | Capturar la pantalla |
| `clipboard_read` / `clipboard_write` | Acceso al portapapeles |
| `volume` | Control del volumen del sistema |
| `system_info` | Información del hardware y sistema |
| `list_windows` / `minimize_window` / `maximize_window` | Gestión de ventanas |
| `type_text` | Escribir texto en la ventana activa |
| `create_folder` | Crear directorios |
| `open_folder` | Abrir carpetas en el explorador |

### Otras funcionalidades
- Tema oscuro con color de acento personalizable
- Historial de chat persistente
- Hotkeys globales configurables
- Escáner de sistema en primer arranque (detecta apps, navegadores y carpetas)
- Confirmación interactiva antes de ejecutar acciones sensibles
- Crash logs automáticos en `data/crash_*.log`

## Requisitos

- Windows 10 / 11 (x64)
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Ollama](https://ollama.ai) instalado y corriendo localmente

## Instalación

### 1. Clonar el repositorio
```bash
git clone https://github.com/tu-usuario/ares.git
cd ares
```

### 2. Instalar Ollama y un modelo
```bash
# Instalar Ollama desde https://ollama.ai
ollama pull qwen2.5:32b
```
Puedes usar cualquier modelo compatible. Por defecto ARES usa `qwen2.5:32b`.

### 3. Compilar y ejecutar
```bash
dotnet build -c Release
dotnet run --project AresAssistant
```
O abrir `Ares.sln` en Visual Studio 2022+ y ejecutar.

## Configuración

Al primer arranque, ARES escanea el sistema para detectar aplicaciones, navegadores y carpetas. Esto crea `data/tools.json`.

La configuración se guarda en `data/config.json` y se puede modificar desde el panel de Ajustes (`⚙` en la barra superior):

| Opción | Valores | Descripción |
|---|---|---|
| `AccentColor` | Hex (`#ff2222`) | Color de acento de la UI |
| `OllamaModel` | ID del modelo | Modelo de Ollama a usar |
| `AssistantName` | String | Nombre del asistente |
| `OverlayPosition` | `bottom-right`, `bottom-left`, `top-right`, `top-left` | Posición del overlay |
| `OverlayOpacity` | `0.3` – `1.0` | Opacidad de la ventana |
| `FontSize` | `small`, `medium`, `large` | Tamaño de fuente |
| `ShowHideHotkey` | Ej: `Ctrl+Space` | Hotkey para mostrar/ocultar |
| `ToggleModeHotkey` | Ej: `Ctrl+Shift+Space` | Hotkey para cambiar modo |
| `SaveChatHistory` | `true` / `false` | Persistencia del historial |
| `LaunchWithWindows` | `true` / `false` | Inicio automático con Windows |

## Hotkeys predeterminadas

| Hotkey | Acción |
|---|---|
| `Ctrl+Space` | Mostrar / ocultar ARES |
| `Ctrl+Shift+Space` | Cambiar entre Overlay y HUD Completo |

## Estructura del proyecto

```
AresAssistant/
├── App.xaml / App.xaml.cs       # Entrada, recursos globales y crash handling
├── Views/                       # Toda la UI (Windows + Controls unificados)
│   ├── MainWindow               # Shell principal, hotkeys, animaciones
│   ├── SettingsWindow           # Panel de ajustes
│   ├── SplashWindow             # Pantalla de carga / primer arranque
│   ├── OverlayModeControl       # UI modo compacto
│   ├── FullHudModeControl       # UI modo HUD completo
│   └── ConfirmationDialog       # Diálogo de confirmación de herramientas
├── ViewModels/                  # MVVM: Chat, Settings, Main, Splash
├── Config/
│   ├── AppConfig.cs             # Record de configuración
│   ├── ConfigManager.cs         # Serialización JSON
│   └── ThemeEngine.cs           # Aplicación dinámica del tema
├── Core/                        # Toda la lógica de negocio y servicios
│   ├── AgentLoop.cs             # Bucle principal de conversación con el modelo
│   ├── OllamaClient.cs          # Cliente HTTP para la API de Ollama
│   ├── ConversationHistory.cs   # Gestión del historial
│   ├── OllamaMessage.cs / OllamaResponse.cs  # Modelos de datos
│   ├── ToolDefinition.cs / ToolResult.cs     # Modelos de herramientas
│   ├── SystemScanner.cs / AppScanner.cs      # Escaneo de sistema
│   ├── BrowserScanner.cs / FolderScanner.cs  # Escaneo de sistema
│   ├── PermissionManager.cs / ActionLogger.cs # Seguridad y logging
│   └── Converters.cs / GlobalHotkeyManager.cs / WindowNativeMethods.cs
└── Tools/                       # Implementaciones de herramientas (ITool)
```

## Datos en tiempo de ejecución

```
data/
├── config.json          # Configuración del usuario
├── chat-history.json    # Historial de conversaciones
├── tools.json           # Herramientas generadas por el escáner
├── logs/                # Logs de AccionLogger
└── crash_*.log          # Crashlogs (uno por sesión)
```

## Stack tecnológico

- **WPF .NET 8** — Framework de UI
- **C# 12** — Namespaces por archivo, records, pattern matching
- **Ollama HTTP API** — Modelos de lenguaje locales
- **Newtonsoft.Json** — Serialización
- **NAudio** — Control de audio
- **MVVM** — Patrón de arquitectura UI

## Licencia

MIT
