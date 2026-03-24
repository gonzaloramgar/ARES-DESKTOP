using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace AresAssistant.Core;

public class GlobalHotkeyManager : IDisposable
{
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;

    private IntPtr _hwnd;
    private HwndSource? _source;
    private readonly Dictionary<int, Action> _hotkeys = new();
    private int _nextId = 9000;

    public void Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);
    }

    public int Register(ModifierKeys modifiers, Key key, Action callback)
    {
        var id = _nextId++;
        uint nativeMod = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) nativeMod |= 0x0001;
        if (modifiers.HasFlag(ModifierKeys.Control)) nativeMod |= 0x0002;
        if (modifiers.HasFlag(ModifierKeys.Shift)) nativeMod |= 0x0004;
        if (modifiers.HasFlag(ModifierKeys.Windows)) nativeMod |= 0x0008;

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        RegisterHotKey(_hwnd, id, nativeMod, vk);
        _hotkeys[id] = callback;
        return id;
    }

    public void Unregister(int id)
    {
        if (_hotkeys.Remove(id))
            UnregisterHotKey(_hwnd, id);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_hotkeys.TryGetValue(id, out var callback))
            {
                callback();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _hotkeys.Keys.ToList())
            Unregister(id);
        _source?.RemoveHook(WndProc);
    }

    /// <summary>Parses a hotkey string like "Ctrl+Space" into modifiers + key.</summary>
    public static (ModifierKeys Modifiers, Key Key) ParseHotkey(string hotkeyString)
    {
        var parts = hotkeyString.Split('+');
        ModifierKeys mods = ModifierKeys.None;
        Key key = Key.None;

        foreach (var part in parts)
        {
            switch (part.Trim().ToLower())
            {
                case "ctrl": mods |= ModifierKeys.Control; break;
                case "alt": mods |= ModifierKeys.Alt; break;
                case "shift": mods |= ModifierKeys.Shift; break;
                case "win": mods |= ModifierKeys.Windows; break;
                default:
                    Enum.TryParse<Key>(part.Trim(), true, out key);
                    break;
            }
        }
        return (mods, key);
    }
}
