# ARES — Autonomous Response Engine System

ARES es un asistente de IA para Windows construido en WPF y .NET 8, que corre sobre modelos de lenguaje locales a través de [Ollama](https://ollama.ai). Diseñado para operar como HUD flotante sobre el escritorio, permite controlar el sistema con lenguaje natural sin salir de lo que estés haciendo.

## Características

### Dos modos de interfaz
- **Modo Overlay** (380×600) — Panel compacto anclado a una esquina de la pantalla
- **Modo HUD Completo** (1200×800) — Interfaz completa con lista de herramientas, chat y estado del sistema

### Herramientas integradas

| Herramienta | Descripción |
|---|---|
| `open_app` | Abre cualquier aplicación instalada por nombre (búsqueda aproximada) |
| `close_app` | Cierra una aplicación por nombre de proceso o ventana |
| `open_folder` | Abre cualquier carpeta en el explorador por nombre (búsqueda aproximada) |
| `create_folder` | Crea directorios con soporte de alias (`Desktop`, `Documents`, etc.) |
| `delete_folder` | Mueve carpetas a la papelera de reciclaje (requiere confirmación) |
| `recycle_bin` | Lista, recupera elementos o vacía la papelera de reciclaje de Windows |
| `read_file` | Lee el contenido de un archivo de texto |
| `write_file` | Escribe o sobreescribe un archivo de texto (requiere confirmación) |
| `run_command` | Ejecuta comandos de consola permitidos (requiere confirmación) |
| `search_web` | Busca en Google en el navegador predeterminado |
| `search_browser` | Busca en un navegador específico detectado en el sistema |
| `take_screenshot` | Captura la pantalla y guarda el resultado |
| `clipboard_read` | Lee el texto del portapapeles |
| `clipboard_write` | Escribe texto en el portapapeles (requiere confirmación) |
| `set_volume` | Ajusta el volumen o silencia el audio del sistema |
| `get_system_info` | Devuelve uso de CPU, RAM, disco, tiempo de actividad y hora |
| `list_open_windows` | Lista las ventanas abiertas en el sistema |
| `minimize_window` | Minimiza una ventana por su título |
| `maximize_window` | Maximiza una ventana por su título |
| `type_text` | Escribe texto en la ventana activa (requiere confirmación) |

### Respuesta en streaming
Las respuestas se muestran token a token en tiempo real. El sistema usa streaming para el primer turno de conversación y cambia automáticamente al modo no-streaming cuando hay llamadas a herramientas pendientes.

### Gestión de memoria de modelo
ARES descarga automáticamente el modelo de la RAM de Ollama tras un período de inactividad configurable (`ModelKeepAliveMinutes`). También descarga el modelo al cerrar la aplicación.

### Otras funcionalidades
- Tema oscuro con **selector de color de acento** personalizable (ColorPicker integrado)
- Opacidad del overlay configurable en tiempo real
- Historial de chat persistente con truncado automático (últimos 30 mensajes)
- Hotkeys globales configurables y rerregistrables sin reiniciar
- Escáner de sistema en primer arranque (detecta apps, navegadores y carpetas)
- Confirmación interactiva antes de ejecutar acciones sensibles (desactivable)
- Auto-unload del modelo tras inactividad configurable
- Icono en la bandeja del sistema con opción de minimizar-a-bandeja al cerrar
- Crash logs automáticos en `data/crash_*.log`
- Debug log de peticiones/respuestas de Ollama en `data/logs/ollama_debug.log` (solo en build Debug)

## Requisitos

- Windows 10 / 11 (x64)
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Ollama](https://ollama.ai) instalado y corriendo localmente

## Modelos compatibles

ARES usa la API nativa de herramientas de Ollama. Solo ciertos modelos generan `tool_calls` de forma fiable:

| Modelo | Tool calling | Tamaño aprox. |
|---|---|---|
| `qwen2.5:7b` | ✅ Muy bueno | ~5 GB |
| `qwen2.5:14b` | ✅ Muy bueno | ~9 GB |
| `qwen2.5:32b` | ✅ Excelente (default) | ~20 GB |
| `llama3.1:8b` | ✅ Bueno | ~5 GB |
| `llama3.2:3b` | ✅ Funcional | ~2 GB |
| `mistral-nemo` | ✅ Bueno | ~7 GB |
| `phi4`, `gemma`, `deepseek-r1` | ❌ No soportado | — |

## Instalación

### 1. Clonar el repositorio
```bash
git clone https://github.com/tu-usuario/ares.git
cd ares
```

### 2. Instalar Ollama y un modelo
```bash
# Instalar Ollama desde https://ollama.ai
ollama pull qwen2.5:7b      # opción ligera
ollama pull qwen2.5:32b     # opción por defecto (requiere ~20 GB RAM/VRAM)
```

### 3. Compilar y ejecutar
```bash
dotnet build -c Release
dotnet run --project AresAssistant
```
O abrir `Ares.sln` en Visual Studio 2022+ y ejecutar.

### 4. Generar ejecutable
```bash
dotnet publish "AresAssistant/AresAssistant.csproj" -c Release -r win-x64 --self-contained false -o "Build/"
```
Requiere .NET 8 instalado en la máquina de destino. Resultado: `Build/AresAssistant.exe`.

## Configuración

Al primer arranque, ARES escanea el sistema para detectar aplicaciones, navegadores y carpetas. Esto crea `data/tools.json`.

La configuración se guarda en `data/config.json` y se puede modificar desde el panel de Ajustes:

| Opción | Valores | Descripción |
|---|---|---|
| `AccentColor` | Hex (`#ff2222`) | Color de acento de la UI |
| `OverlayOpacity` | `0.3` – `1.0` | Opacidad del panel overlay |
| `FontSize` | `small`, `medium`, `large` | Tamaño de fuente |
| `OverlayPosition` | `bottom-right`, `bottom-left`, `top-right`, `top-left` | Posición del overlay |
| `OllamaModel` | ID del modelo | Modelo de Ollama a usar |
| `AssistantName` | String | Nombre del asistente |
| `Personality` | `formal`, `casual`, `sarcastico`, `tecnico` | Tono del asistente |
| `ResponseLength` | `normal`, `conciso`, `detallado` | Longitud de respuestas |
| `ShowHideHotkey` | Ej: `Ctrl+Space` | Hotkey para mostrar/ocultar |
| `ToggleModeHotkey` | Ej: `Ctrl+Shift+Space` | Hotkey para cambiar modo |
| `SaveChatHistory` | `true` / `false` | Persistencia del historial |
| `LaunchWithWindows` | `true` / `false` | Inicio automático con Windows |
| `CloseToTray` | `true` / `false` | Minimizar a bandeja al cerrar |
| `ConfirmationAlertsEnabled` | `true` / `false` | Diálogos de confirmación |
| `ModelKeepAliveMinutes` | Entero (`0` = nunca) | Minutos de inactividad antes de descargar el modelo |

## Hotkeys predeterminadas

| Hotkey | Acción |
|---|---|
| `Ctrl+Space` | Mostrar / ocultar ARES |
| `Ctrl+Shift+Space` | Cambiar entre Overlay y HUD Completo |

## Estructura del proyecto

```
AresAssistant/
├── App.xaml / App.xaml.cs        # Entrada, recursos globales, tray icon, crash handling
├── Views/                         # UI completa (Windows y Controles)
│   ├── MainWindow                 # Shell principal, hotkeys, animaciones, idle timer
│   ├── SettingsWindow             # Panel de ajustes
│   ├── SplashWindow               # Pantalla de carga / primer arranque
│   ├── ColorPickerWindow          # Selector de color de acento
│   ├── OverlayModeControl         # UI modo compacto
│   ├── FullHudModeControl         # UI modo HUD completo
│   └── ConfirmationDialog         # Diálogo de confirmación de herramientas
├── ViewModels/                    # MVVM: Chat, Settings, Main, Splash
├── Config/
│   ├── AppConfig.cs               # Record de configuración
│   ├── ConfigManager.cs           # Serialización JSON
│   └── ThemeEngine.cs             # Aplicación dinámica del tema
├── Core/                          # Lógica de negocio y servicios
│   ├── AgentLoop.cs               # Bucle de conversación (streaming + tool-call loop)
│   ├── OllamaClient.cs            # Cliente HTTP: ChatAsync, ChatStreamAsync, UnloadModelAsync
│   ├── ConversationHistory.cs     # Gestión del historial con TrimToLast
│   ├── OllamaMessage.cs           # Modelos de datos de mensajes
│   ├── OllamaResponse.cs          # Respuesta + ToolArgumentsConverter (string/object)
│   ├── ToolDefinition.cs          # Esquemas de herramientas (compatible OpenAI)
│   ├── SystemScanner.cs           # Coordinador de escaneo
│   ├── AppScanner / BrowserScanner / FolderScanner
│   ├── PermissionManager.cs       # Niveles Auto / Confirm / Blocked por herramienta
│   ├── ActionLogger.cs            # Log de acciones ejecutadas
│   └── GlobalHotkeyManager.cs     # Hotkeys globales Win32
└── Tools/                         # Implementaciones ITool
    ├── PathResolver.cs             # Resolución de alias de rutas (compartido)
    ├── GenericOpenAppTool.cs       # open_app con búsqueda aproximada
    ├── GenericOpenFolderTool.cs    # open_folder con búsqueda aproximada
    ├── CreateFolderTool.cs
    ├── DeleteFolderTool.cs         # Mueve a papelera (requiere confirmación)
    ├── RecycleBinTool.cs           # list / restore / restore_all (lee $I* metadata)
    ├── [resto de tools individuales]
    ├── ToolRegistry.cs             # Registro y carga desde tools.json
    └── ToolDispatcher.cs           # Permisos, confirmación y ejecución
```

## Datos en tiempo de ejecución

```
data/
├── config.json           # Configuración del usuario
├── chat-history.json     # Historial de conversaciones
├── tools.json            # Herramientas generadas por el escáner de primer arranque
├── logs/
│   ├── actions_*.log     # Log de acciones ejecutadas por el asistente
│   └── ollama_debug.log  # Log de peticiones/respuestas (solo build Debug)
└── crash_*.log           # Crash logs (uno por sesión)
```

## Stack tecnológico

- **WPF .NET 8** — Framework de UI
- **C# 12** — Namespaces por archivo, records, pattern matching, `IAsyncEnumerable`
- **Ollama HTTP API** — Modelos de lenguaje locales (streaming + tool calling)
- **Newtonsoft.Json** — Serialización
- **NAudio** — Control de audio
- **System.Windows.Forms** — NotifyIcon para la bandeja del sistema
- **Microsoft.VisualBasic.FileIO** — Operaciones de papelera de reciclaje
- **MVVM** — Patrón de arquitectura UI

## Licencia

MIT
