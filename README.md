# ARES — Autonomous Response Engine System

ARES es un asistente de IA para Windows construido en WPF y .NET 8, que corre sobre modelos de lenguaje locales a través de [Ollama](https://ollama.ai). Diseñado para operar como HUD flotante sobre el escritorio, permite controlar el sistema con lenguaje natural sin salir de lo que estés haciendo.

## Características

### Dos modos de interfaz
- **Modo Overlay** (380×600) — Panel compacto anclado a una esquina de la pantalla
- **Modo HUD Completo** (1200×800) — Interfaz completa con lista de herramientas, chat y estado del sistema

### Síntesis de voz (TTS) — Sistema de 3 niveles
ARES puede leer sus respuestas en voz alta con un sistema de TTS de 3 niveles con fallback automático:

| Nivel | Motor | Tipo | Requisitos |
|---|---|---|---|
| 1 | **Piper** | Neural offline | Se descarga automáticamente (~60 MB). Mejor calidad |
| 2 | **Edge TTS** | Neural online | Requiere conexión a internet |
| 3 | **Windows OneCore** | Local del sistema | Siempre disponible como último recurso |

- **Selección de género de voz** — Masculino (Piper `davefx` → Edge Álvaro → WinRT Pablo) o Femenino (Piper `sharvard` speaker F → Edge Dalia/Elvira → SAPI femenino)
- **Control de volumen** en tiempo real desde Ajustes
- **Botón de prueba** para escuchar la voz configurada
- Configurable desde el asistente de configuración inicial y desde Ajustes

### Asistente de configuración inicial (Setup Wizard)
Al primer arranque, ARES presenta un wizard de 5 pasos:
1. **Bienvenida** — Presentación del asistente
2. **Nombre y personalidad** — Configura nombre, tono y longitud de respuestas
3. **Rendimiento** — Detección automática de hardware y recomendación de modo
4. **Voz** — Activar/desactivar TTS, seleccionar género, ajustar volumen y probar
5. **Apariencia** — Color de acento, opacidad y posición del overlay

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
| `remember_app` | Guarda nombre + ruta de una app no detectada automáticamente (persistente) |

### Escaneo de aplicaciones inteligente
- **Registry + Start Menu** — Detecta aplicaciones instaladas vía registro de Windows y accesos directos del menú Inicio
- **Steam** — Escanea todas las bibliotecas de Steam (parsea `libraryfolders.vdf`) y encuentra el ejecutable correcto de cada juego
- **Epic Games** — Lee los manifiestos `.item` de Epic Games Launcher para detectar juegos instalados (Fortnite, etc.)
- **Escritorio** — Escanea accesos directos `.lnk` y `.url` (incluye URLs `steam://`)
- **Memoria de apps personalizadas** — Si un juego o app no se detecta, el usuario proporciona la ruta una vez y ARES la recuerda para siempre (`data/custom-apps.json`). La herramienta `remember_app` permite al modelo guardar nuevas apps durante la conversación

### Respuesta en streaming
Las respuestas se muestran token a token en tiempo real. El sistema usa streaming para el primer turno de conversación y cambia automáticamente al modo no-streaming cuando hay llamadas a herramientas pendientes.

### Modos de rendimiento
- **Ligero** — `num_ctx=4096`, 4 threads, 20 mensajes de historial. Ideal para hardware modesto (16 GB RAM, CPU básica)
- **Avanzado** — `num_ctx=8192`, threads automáticos, 30 mensajes de historial. Para hardware potente

### Gestión de memoria de modelo
ARES descarga automáticamente el modelo de la RAM de Ollama tras un período de inactividad configurable (`ModelKeepAliveMinutes`). También descarga el modelo al cerrar la aplicación.

### Seguridad y estabilidad
- **Canonicalización de rutas** — `PermissionManager` y `DeleteFolderTool` usan `Path.GetFullPath()` para prevenir bypass por traversal (`C:\Windows\..\..\target`)
- **Timeout de comandos** — `RunCommandTool` mata procesos que excedan 30 segundos
- **Patrones bloqueados ampliados** — `powershell -command`, `python -c`, `downloadstring`, `downloadfile`, `set-executionpolicy`, etc.
- **Thread safety** — `ConversationHistory` protegida con locks en todas las operaciones
- **I/O asíncrono** — `ReadFileTool`, `WriteFileTool` y `SystemInfoTool` usan operaciones async para no bloquear el hilo de UI
- **Ejecución de herramientas** fuera del hilo de UI (`Task.Run` en `ToolDispatcher`)

### Otras funcionalidades
- Tema oscuro con **selector de color de acento** personalizable (ColorPicker integrado)
- Opacidad del overlay configurable en tiempo real
- Historial de chat persistente con truncado automático
- Hotkeys globales configurables y rerregistrables sin reiniciar
- Escáner de sistema en cada arranque (detecta apps de Steam, Epic Games, escritorio, navegadores y carpetas)
- Purga automática de historial envenenado (elimina respuestas "app no encontrada" obsoletas al iniciar)
- Parámetros de inferencia anti-alucinación (`temperature: 0.7`, `repeat_penalty: 1.1`)
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

| Modelo | Tool calling | Tamaño aprox. | Notas |
|---|---|---|---|
| `qwen2.5:7b` | ✅ Muy bueno | ~5 GB | **Recomendado** — Mejor relación calidad/velocidad. Default |
| `qwen2.5:14b` | ✅ Muy bueno | ~9 GB | **Recomendado** — Mejor calidad de respuesta, más lento. Requiere ~16 GB RAM |
| `qwen2.5:32b` | ✅ Excelente | ~20 GB | Requiere GPU potente |
| `llama3.1:8b` | ✅ Bueno | ~5 GB | |
| `llama3.2:3b` | ✅ Funcional | ~2 GB | Tool-calling poco fiable |
| `mistral-nemo` | ✅ Bueno | ~7 GB | |
| `phi4`, `gemma`, `deepseek-r1` | ❌ No soportado | — | No generan `tool_calls` |

> **qwen2.5:7b** es el modelo por defecto y el más probado con ARES. **qwen2.5:14b** ofrece respuestas notablemente mejores y más fiables en tool-calling si el hardware lo permite (recomendado con 16+ GB de RAM o GPU con 10+ GB VRAM).

## Instalación

### 1. Clonar el repositorio
```bash
git clone https://github.com/tu-usuario/ares.git
cd ares
```

### 2. Instalar Ollama y un modelo
```bash
# Instalar Ollama desde https://ollama.ai
ollama pull qwen2.5:7b      # recomendado — rápido, ~5 GB, funciona en cualquier PC
ollama pull qwen2.5:14b     # mejor calidad — ~9 GB, recomendado con 16+ GB RAM
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
| `PerformanceMode` | `ligero`, `avanzado` | Modo de rendimiento (control de contexto, threads e historial) |
| `ModelKeepAliveMinutes` | Entero (`0` = nunca) | Minutos de inactividad antes de descargar el modelo |
| `VoiceEnabled` | `true` / `false` | Activar síntesis de voz (TTS) |
| `TtsVoiceGender` | `masculino`, `femenino` | Género de la voz del asistente |
| `TtsVolume` | `0.0` – `1.0` | Volumen de la voz |

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
│   ├── SetupWindow                 # Wizard de configuración inicial (5 pasos)
│   ├── OverlayModeControl         # UI modo compacto
│   ├── FullHudModeControl         # UI modo HUD completo
│   ├── ConfirmationDialog         # Diálogo de confirmación de herramientas
│   └── PurgeConfirmationDialog    # Confirmación de purga de historial
├── ViewModels/                    # MVVM: Chat, Settings, Main, Splash
│   ├── ViewModelBase.cs           # Base INotifyPropertyChanged
│   ├── ChatViewModel.cs           # Estado del chat y mensajes
│   ├── SettingsViewModel.cs       # Todas las opciones con live-apply
│   ├── MainViewModel.cs           # Toggle Overlay / HUD
│   └── SplashViewModel.cs         # Progreso de carga
├── Config/
│   ├── AppConfig.cs               # Record de configuración
│   ├── ConfigManager.cs           # Serialización JSON
│   └── ThemeEngine.cs             # Aplicación dinámica del tema
├── Core/                          # Lógica de negocio y servicios
│   ├── AgentLoop.cs               # Bucle de conversación (streaming + tool-call loop, max 10 iteraciones)
│   ├── OllamaClient.cs            # Cliente HTTP: ChatAsync, ChatStreamAsync, UnloadModelAsync
│   ├── ConversationHistory.cs     # Gestión del historial thread-safe con TrimToLast
│   ├── SpeechEngine.cs            # TTS 3 niveles: Piper neural → Edge online → Windows local
│   ├── OllamaMessage.cs           # Modelos de datos de mensajes
│   ├── OllamaResponse.cs          # Respuesta + ToolArgumentsConverter (string/object)
│   ├── ToolDefinition.cs          # Esquemas de herramientas (compatible OpenAI)
│   ├── SystemScanner.cs           # Coordinador de escaneo
│   ├── AppScanner.cs              # Escaneo: Registry, Start Menu, Steam, Epic Games, Desktop, custom apps
│   ├── BrowserScanner.cs / FolderScanner.cs
│   ├── PermissionManager.cs       # Niveles Auto / Confirm / Blocked con canonicalización de rutas
│   ├── ActionLogger.cs            # Log de acciones ejecutadas
│   ├── HardwareDetector.cs        # Detección de CPU/RAM para recomendación de modo
│   ├── StartupManager.cs          # Autoarranque con Windows (registro HKCU)
│   └── GlobalHotkeyManager.cs     # Hotkeys globales Win32
└── Tools/                         # Implementaciones ITool
    ├── PathResolver.cs             # Resolución de alias de rutas (compartido)
    ├── GenericOpenAppTool.cs       # open_app con búsqueda aproximada
    ├── GenericOpenFolderTool.cs    # open_folder con búsqueda aproximada
    ├── RememberAppTool.cs         # Guarda apps custom en data/custom-apps.json
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
├── tools.json            # Herramientas generadas por el escáner (se regenera cada arranque)
├── custom-apps.json      # Apps/juegos guardados manualmente por el usuario (persistente)
├── tts/                  # Archivos de Piper TTS (descargados automáticamente)
├── logs/
│   ├── actions_*.log     # Log de acciones ejecutadas por el asistente
│   └── ollama_debug.log  # Log de peticiones/respuestas (solo build Debug)
└── crash_*.log           # Crash logs (uno por sesión)
```

## Stack tecnológico

- **WPF .NET 8** (TFM: `net8.0-windows10.0.19041.0`) — Framework de UI
- **C# 12** — Namespaces por archivo, records, pattern matching, `IAsyncEnumerable`
- **Ollama HTTP API** — Modelos de lenguaje locales (streaming + tool calling)
- **Newtonsoft.Json** — Serialización
- **NAudio 2.2.1** — Control de volumen del sistema + reproducción de audio TTS
- **Piper TTS** — Motor de síntesis neural offline (descarga automática)
- **Edge TTS** — Síntesis neural online vía WebSocket (voces de Microsoft Edge)
- **Windows.Media.SpeechSynthesis** — WinRT OneCore como fallback local (voz masculina)
- **System.Speech** — SAPI 5 como fallback local femenino (`SelectVoiceByHints`)
- **System.Windows.Forms** — NotifyIcon para la bandeja del sistema
- **Microsoft.VisualBasic.FileIO** — Operaciones de papelera de reciclaje
- **System.Management** — Detección de hardware (CPU, RAM) para modos de rendimiento
- **System.Drawing.Common** — Capturas de pantalla
- **MVVM** — Patrón de arquitectura UI

## Licencia

MIT
