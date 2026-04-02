# ARES — Autonomous Response Engine System

ARES es un asistente de IA para Windows construido en WPF y .NET 8, que corre sobre modelos de lenguaje locales a través de [Ollama](https://ollama.ai). Diseñado para operar como HUD flotante sobre el escritorio, permite controlar el sistema con lenguaje natural sin salir de lo que estés haciendo.

---

## Interfaz

### Dos modos de visualización
- **Modo Overlay** (380×600) — Panel compacto anclado a una esquina de la pantalla. Muestra el chat y el indicador de actividad de forma no intrusiva
- **Modo HUD Completo** (1200×800) — Interfaz completa con tres columnas: lista de herramientas disponibles, chat central y dashboard de información

### Estética glassmorphism
Toda la interfaz usa un sistema de diseño cohesivo oscuro con efectos de cristal:
- Fondos con gradientes profundos (`#0e0e18` → `#06060c`)
- Paneles translúcidos (`GlassBrush` + `GlassBorderBrush`) sobre las superficies
- Línea de brillo de acento en la parte superior de cada ventana
- Icono diamante como identificador visual del asistente
- Animaciones de entrada (fade + slide) en todas las ventanas y diálogos
- **Color de acento completamente personalizable** — afecta bordes, iconos, botones y efectos de brillo en toda la UI en tiempo real sin reinicio

### Dashboard (HUD Completo — panel derecho)
El panel derecho del HUD Completo muestra información en tiempo real mediante widgets configurables:

| Widget | Contenido |
|---|---|
| **Reloj local** | Hora actual actualizada cada segundo |
| **Estado del asistente** | Texto de actividad (Pensando / Esperando...) |
| **Clima** | Temperatura, descripción y humedad de la ciudad configurada (wttr.in) |
| **Hora mundial** | Hora simultánea en zonas horarias adicionales |
| **Sistema en vivo** | Uso de CPU % y RAM usada/total en tiempo real |
| **Tareas rápidas** | Bloc de notas integrado persistente |

### Badge de modelo por mensaje
Cada mensaje del asistente muestra una etiqueta discreta con el modelo de IA que generó la respuesta, visible tanto en Overlay como en HUD Completo.

---

## Síntesis de voz (TTS) — Sistema de 3 niveles

ARES puede leer sus respuestas en voz alta con fallback automático:

| Nivel | Motor | Tipo | Requisitos |
|---|---|---|---|
| 1 | **Piper** | Neural offline | Se descarga automáticamente (~60 MB). Mejor calidad |
| 2 | **Edge TTS** | Neural online | Requiere conexión a internet |
| 3 | **Windows OneCore** | Local del sistema | Siempre disponible como último recurso |

- **Selección de género de voz** — Masculino (Piper `davefx` → Edge Álvaro → WinRT Pablo) o Femenino (Piper `sharvard` speaker F → Edge Dalia/Elvira → SAPI femenino)
- **Control de volumen** en tiempo real desde Ajustes
- **Botón de prueba** para escuchar la voz configurada

---

## Asistente de configuración inicial (Setup Wizard)

Al primer arranque, ARES presenta un wizard de 5 pasos con animaciones de transición entre páginas:

1. **Bienvenida + Nombre** — Presentación del asistente y nombre personalizado
2. **Voz** — Activar/desactivar TTS, seleccionar género, ajustar volumen, probar voz y descargar voces Piper offline
3. **Inteligencia Artificial** — Personalidad, longitud de respuestas, detección de hardware, selección de modo de rendimiento (Ligero/Avanzado) e instalación automática de Ollama + modelo
4. **Apariencia** — Color de acento, opacidad, tamaño de fuente y posición del overlay
5. **Sistema** — Hotkeys, inicio con Windows, historial, bandeja del sistema y tiempo de descarga del modelo

### Instalación automática de Ollama
Desde el wizard, el botón **"Instalar todo"** abre `OllamaInstallWindow` con 3 fases:
1. **Descarga** del instalador desde ollama.com (0–60%) — muestra MB descargados en tiempo real
2. **Instalación** silenciosa con elevación de permisos UAC (60–80%)
3. **Espera** a que la API de Ollama responda (80–100%)

Una vez instalado, descarga automáticamente el modelo correspondiente al modo elegido mediante `ModelDownloadWindow`, que muestra porcentaje, tamaño descargado/total y estado de cada capa.

---

## Rendimiento de IA

### Modos de rendimiento

| Parámetro | Ligero (`qwen2.5:7b`) | Avanzado (`qwen2.5:14b`) |
|---|---|---|
| `num_ctx` | 4096 | 4096 |
| `num_predict` | 512 | 512 |
| `num_batch` | 512 | 1024 |
| `num_thread` | auto | auto |
| Historial máximo | 20 mensajes | 20 mensajes |

- **Ligero** — Modelo 7b, rápido, ideal para hardware modesto (8–16 GB RAM)
- **Avanzado** — Modelo 14b, mejor calidad de respuesta y tool-calling. Requiere ~16 GB RAM o GPU con 10+ GB VRAM

### Optimizaciones de velocidad
- **Precarga del modelo** al iniciar — `PreloadModelAsync` carga el modelo en RAM sin hacer inferencia, eliminando el cold-start del primer mensaje
- **`keep_alive` configurable** — El modelo permanece en RAM entre mensajes (por defecto 30 min), configurable desde Ajustes
- **Buffer de streaming reducido** — Los primeros tokens aparecen en UI en menos de 50 caracteres buffereados
- **System prompts diferenciados** — El modelo 7b recibe instrucciones explícitas de mapeo acción→herramienta; el 14b recibe un prompt compacto optimizado para su mayor capacidad

---

## Herramientas integradas

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
| `get_location` | Detecta la ciudad y coordenadas del usuario por IP (sin GPS) |
| `get_weather` | Obtiene el clima actual usando coordenadas (Open-Meteo, sin API key) |

### Escaneo de aplicaciones inteligente
- **Registry + Start Menu** — Detecta aplicaciones instaladas vía registro de Windows y accesos directos del menú Inicio
- **Steam** — Escanea todas las bibliotecas de Steam (parsea `libraryfolders.vdf`) y encuentra el ejecutable correcto de cada juego
- **Epic Games** — Lee los manifiestos `.item` para detectar juegos instalados
- **Escritorio** — Escanea accesos directos `.lnk` y `.url` (incluye URLs `steam://`)
- **Memoria de apps personalizadas** — Si una app no se detecta, el usuario proporciona la ruta una vez y ARES la recuerda para siempre (`data/custom-apps.json`)

---

## Seguridad y estabilidad

- **Canonicalización de rutas** — `PermissionManager` usa `Path.GetFullPath()` para prevenir path traversal
- **Timeout de comandos** — `RunCommandTool` mata procesos que excedan 30 segundos
- **Patrones bloqueados** — `powershell -command`, `python -c`, `downloadstring`, `set-executionpolicy`, etc.
- **Thread safety** — `ConversationHistory` protegida con locks en todas las operaciones
- **I/O asíncrono** — Herramientas de ficheros usan `async/await` para no bloquear la UI
- **Ejecución de herramientas** fuera del hilo de UI (`Task.Run` en `ToolDispatcher`)
- **Crash logs automáticos** en `data/crash_*.log` (uno por sesión)

---

## Otras funcionalidades

- **Bandeja del sistema** — Icono en la barra de tareas con menú contextual (Abrir / Salir). Comportamiento de "minimizar a bandeja" al cerrar configurable
- **Hotkeys globales** configurables y rerregistrables sin reiniciar
- Historial de chat persistente con truncado automático
- Purga automática de historial envenenado (elimina respuestas "app no encontrada" obsoletas al iniciar)
- Parámetros de inferencia anti-alucinación (`temperature: 0.7`, `repeat_penalty: 1.1`)
- Confirmación interactiva antes de ejecutar acciones sensibles (desactivable)
- Escáner de sistema en cada arranque (apps, navegadores, carpetas)
- Debug log en `data/logs/ollama_debug.log` (solo en build Debug)

---

## Requisitos

- Windows 10 / 11 (x64)
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Ollama](https://ollama.ai) instalado y corriendo localmente

---

## Modelos compatibles

ARES usa la API nativa de herramientas de Ollama. Solo ciertos modelos generan `tool_calls` de forma fiable:

| Modelo | Tool calling | Tamaño aprox. | Notas |
|---|---|---|---|
| `qwen2.5:7b` | ✅ Muy bueno | ~5 GB | **Recomendado** — Mejor relación calidad/velocidad. Default |
| `qwen2.5:14b` | ✅ Muy bueno | ~9 GB | **Recomendado** — Mejor calidad, más lento. Requiere ~16 GB RAM |
| `qwen2.5:32b` | ✅ Excelente | ~20 GB | Requiere GPU potente |
| `llama3.1:8b` | ✅ Bueno | ~5 GB | |
| `llama3.2:3b` | ✅ Funcional | ~2 GB | Tool-calling poco fiable |
| `mistral-nemo` | ✅ Bueno | ~7 GB | |
| `phi4`, `gemma`, `deepseek-r1` | ❌ No soportado | — | No generan `tool_calls` |

> **qwen2.5:7b** es el modelo por defecto y el más probado con ARES. **qwen2.5:14b** ofrece respuestas notablemente mejores si el hardware lo permite (recomendado con 16+ GB RAM o GPU con 10+ GB VRAM).

---

## Instalación

### Opción A — Ejecutable precompilado (recomendado)
1. Descarga el zip de la última [release](https://github.com/gonzaloramgar/ARES/releases)
2. Extrae y ejecuta `AresAssistant.exe`
3. Al primer arranque, ARES instala Ollama y descarga el modelo automáticamente

Requisito: [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) instalado en la máquina.

### Opción B — Compilar desde fuente
```bash
git clone https://github.com/gonzaloramgar/ARES.git
cd ARES
dotnet publish "AresAssistant/AresAssistant.csproj" -c Release -r win-x64 --self-contained false -o "Build/"
```
Resultado: `Build/AresAssistant.exe`.

---

## Configuración

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
| `PerformanceMode` | `ligero`, `avanzado` | Modo de rendimiento |
| `ModelKeepAliveMinutes` | Entero (`0` = nunca) | Minutos antes de descargar el modelo de RAM |
| `VoiceEnabled` | `true` / `false` | Activar síntesis de voz (TTS) |
| `TtsVoiceGender` | `masculino`, `femenino` | Género de la voz |
| `TtsVolume` | `0.0` – `1.0` | Volumen de la voz |

## Hotkeys predeterminadas

| Hotkey | Acción |
|---|---|
| `Ctrl+Space` | Mostrar / ocultar ARES |
| `Ctrl+Shift+Space` | Cambiar entre Overlay y HUD Completo |

---

## Estructura del proyecto

```
AresAssistant/
├── App.xaml / App.xaml.cs        # Entrada, recursos globales, tray icon, crash handling
├── Views/
│   ├── MainWindow                 # Shell principal, hotkeys, animaciones, idle timer
│   ├── SettingsWindow             # Panel de ajustes completo
│   ├── SplashWindow               # Pantalla de carga animada (radar, órbitas, scanline)
│   ├── ColorPickerWindow          # Selector HSV de color de acento
│   ├── SetupWindow                # Wizard de configuración inicial (5 pasos)
│   ├── OverlayModeControl         # UI modo compacto con badge de modelo
│   ├── FullHudModeControl         # UI HUD completo con dashboard de widgets
│   ├── OllamaInstallWindow        # Instalación automática de Ollama (3 fases)
│   ├── ModelDownloadWindow        # Descarga de modelos Ollama con progreso en tiempo real
│   ├── UpdateWindow               # Comprobación e instalación de actualizaciones
│   ├── ConfirmationDialog         # Diálogo de confirmación de acciones
│   ├── PurgeConfirmationDialog    # Confirmación de purga de datos
│   └── AresMessageBox             # Diálogo de mensajes/alertas del sistema
├── ViewModels/
│   ├── ViewModelBase.cs           # Base INotifyPropertyChanged
│   ├── ChatViewModel.cs           # Estado del chat, mensajes y streaming
│   ├── SettingsViewModel.cs       # Todas las opciones con live-apply
│   ├── MainViewModel.cs           # Toggle Overlay / HUD
│   └── SplashViewModel.cs        # Progreso de carga
├── Helpers/
│   └── FormatHelper.cs            # Formateo de bytes (B/KB/MB/GB)
├── Config/
│   ├── AppConfig.cs               # Record de configuración completo
│   ├── ConfigManager.cs           # Serialización JSON
│   └── ThemeEngine.cs             # Aplicación dinámica del tema de acento
├── Core/
│   ├── AgentLoop.cs               # Bucle de conversación (streaming + tool-call loop)
│   ├── OllamaClient.cs            # Cliente HTTP: Chat, Stream, Unload, Pull, Preload
│   ├── ConversationHistory.cs     # Historial thread-safe con TrimToLast
│   ├── SpeechEngine.cs            # TTS 3 niveles: Piper → Edge → Windows
│   ├── OllamaMessage.cs           # Modelos de datos de mensajes
│   ├── OllamaResponse.cs          # Respuesta + ToolArgumentsConverter
│   ├── ToolDefinition.cs          # Esquemas de herramientas (compatible OpenAI)
│   ├── SystemScanner.cs           # Coordinador de escaneo de sistema
│   ├── AppScanner.cs              # Registry, Start Menu, Steam, Epic, Desktop
│   ├── BrowserScanner.cs / FolderScanner.cs
│   ├── PermissionManager.cs       # Niveles Auto / Confirm / Blocked
│   ├── ActionLogger.cs            # Log de acciones ejecutadas
│   ├── HardwareDetector.cs        # Detección de CPU/RAM
│   ├── StartupManager.cs          # Autoarranque con Windows (HKCU)
│   ├── GlobalHotkeyManager.cs     # Hotkeys globales Win32
│   └── WindowNativeMethods.cs     # P/Invoke para operaciones de ventanas
└── Tools/
    ├── PathResolver.cs
    ├── GenericOpenAppTool.cs
    ├── GenericOpenFolderTool.cs
    ├── RememberAppTool.cs
    ├── CreateFolderTool.cs
    ├── DeleteFolderTool.cs
    ├── RecycleBinTool.cs
    ├── LocationTool.cs
    ├── WeatherTool.cs
    ├── ClipboardTools.cs
    ├── WindowTools.cs
    ├── VolumeTool.cs
    ├── ToolRegistry.cs
    └── ToolDispatcher.cs
```

## Datos en tiempo de ejecución

```
data/
├── config.json           # Configuración del usuario
├── chat-history.json     # Historial de conversaciones
├── tools.json            # Herramientas del escáner (se regenera cada arranque)
├── custom-apps.json      # Apps guardadas manualmente (persistente)
├── tts/                  # Modelos Piper TTS (descargados automáticamente)
├── logs/
│   ├── actions_*.log     # Log de acciones ejecutadas
│   └── ollama_debug.log  # Log de peticiones/respuestas (solo Debug)
└── crash_*.log           # Crash logs (uno por sesión)
```

---

## Stack tecnológico

- **WPF .NET 8** (`net8.0-windows10.0.19041.0`) — Framework de UI
- **C# 12** — Namespaces por archivo, records, pattern matching, `IAsyncEnumerable`
- **Ollama HTTP API** — Modelos de lenguaje locales (streaming + tool calling)
- **Newtonsoft.Json** — Serialización
- **NAudio 2.2.1** — Control de volumen del sistema + reproducción de audio TTS
- **Piper TTS** — Motor de síntesis neural offline
- **Edge TTS** — Síntesis neural online vía WebSocket
- **Windows.Media.SpeechSynthesis** — WinRT OneCore (fallback local masculino)
- **System.Speech** — SAPI 5 (fallback local femenino)
- **System.Windows.Forms** — NotifyIcon para bandeja del sistema
- **Microsoft.VisualBasic.FileIO** — Operaciones de papelera de reciclaje
- **System.Management** — Detección de hardware (CPU, RAM)
- **System.Drawing.Common** — Capturas de pantalla
- **MVVM** — Patrón de arquitectura UI

---

## Licencia

MIT
