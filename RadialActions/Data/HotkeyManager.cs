using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace RadialActions;

/// <summary>
/// Manages global hotkey registration for the application.
/// </summary>
public class HotkeyManager : IDisposable
{
    private readonly IntPtr _windowHandle;
    private readonly HwndSource _source;
    private const int WM_HOTKEY = 0x0312;
    private int _currentId;
    private readonly ConcurrentDictionary<string, int> _hotkeys = new();
    private bool _disposed;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// Occurs when a registered hotkey is pressed.
    /// </summary>
    public event EventHandler HotkeyPressed;

    public HotkeyManager(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(HwndHook);
    }

    /// <summary>
    /// Registers a hotkey string (e.g., "Ctrl+Alt+Space").
    /// </summary>
    /// <param name="hotkey">The hotkey string to register.</param>
    /// <returns>True if registration succeeded.</returns>
    public bool RegisterHotkey(string hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey))
        {
            Log.Warning("Cannot register empty hotkey");
            return false;
        }

        // Unregister existing hotkey with same string if any
        if (_hotkeys.TryGetValue(hotkey, out var existingId))
        {
            UnregisterHotKey(_windowHandle, existingId);
            _hotkeys.TryRemove(hotkey, out _);
            Log.Debug($"Unregistered existing hotkey: {hotkey}");
        }

        var (modifiers, keyCode) = ParseHotkey(hotkey);

        if (keyCode == 0)
        {
            Log.Warning($"Could not parse hotkey: {hotkey}");
            return false;
        }

        var id = ++_currentId;

        if (RegisterHotKey(_windowHandle, id, modifiers, keyCode))
        {
            _hotkeys[hotkey] = id;
            Log.Information($"Registered hotkey: {hotkey} (ID: {id})");
            return true;
        }
        else
        {
            Log.Error($"Failed to register hotkey: {hotkey}. It may be in use by another application.");
            return false;
        }
    }

    /// <summary>
    /// Unregisters a hotkey by its string representation.
    /// </summary>
    public bool UnregisterHotkey(string hotkey)
    {
        if (_hotkeys.TryRemove(hotkey, out var id))
        {
            var result = UnregisterHotKey(_windowHandle, id);
            Log.Debug($"Unregistered hotkey: {hotkey} (success: {result})");
            return result;
        }
        return false;
    }

    /// <summary>
    /// Unregisters all hotkeys.
    /// </summary>
    public void UnregisterAll()
    {
        foreach (var kvp in _hotkeys)
        {
            UnregisterHotKey(_windowHandle, kvp.Value);
            Log.Debug($"Unregistered hotkey: {kvp.Key}");
        }
        _hotkeys.Clear();
    }

    private (uint modifiers, uint keyCode) ParseHotkey(string hotkey)
    {
        uint modifiers = 0;
        uint keyCode = 0;

        var parts = hotkey.Split('+');
        foreach (var part in parts)
        {
            var trimmed = part.Trim().ToLower();
            switch (trimmed)
            {
                case "ctrl":
                case "control":
                    modifiers |= 0x0002; // MOD_CONTROL
                    break;
                case "alt":
                    modifiers |= 0x0001; // MOD_ALT
                    break;
                case "shift":
                    modifiers |= 0x0004; // MOD_SHIFT
                    break;
                case "win":
                case "windows":
                    modifiers |= 0x0008; // MOD_WIN
                    break;
                default:
                    keyCode = GetVirtualKeyCode(trimmed);
                    break;
            }
        }

        return (modifiers, keyCode);
    }

    private static uint GetVirtualKeyCode(string keyName)
    {
        // Handle function keys F1-F24
        if (keyName.StartsWith("f") && int.TryParse(keyName.Substring(1), out var fNum) && fNum >= 1 && fNum <= 24)
            return (uint)(0x6F + fNum); // VK_F1 = 0x70

        // Handle single alphanumeric characters
        if (keyName.Length == 1)
        {
            var c = char.ToUpper(keyName[0]);
            if (c >= 'A' && c <= 'Z')
                return c;
            if (c >= '0' && c <= '9')
                return c;
        }

        // Handle special keys
        return keyName switch
        {
            "space" => 0x20,
            "enter" or "return" => 0x0D,
            "tab" => 0x09,
            "backspace" or "back" => 0x08,
            "delete" or "del" => 0x2E,
            "insert" or "ins" => 0x2D,
            "home" => 0x24,
            "end" => 0x23,
            "pageup" or "pgup" => 0x21,
            "pagedown" or "pgdn" => 0x22,
            "up" => 0x26,
            "down" => 0x28,
            "left" => 0x25,
            "right" => 0x27,
            "escape" or "esc" => 0x1B,
            "printscreen" or "prtsc" => 0x2C,
            "scrolllock" => 0x91,
            "pause" => 0x13,
            "numlock" => 0x90,
            "capslock" => 0x14,
            "numpad0" => 0x60,
            "numpad1" => 0x61,
            "numpad2" => 0x62,
            "numpad3" => 0x63,
            "numpad4" => 0x64,
            "numpad5" => 0x65,
            "numpad6" => 0x66,
            "numpad7" => 0x67,
            "numpad8" => 0x68,
            "numpad9" => 0x69,
            "multiply" => 0x6A,
            "add" => 0x6B,
            "subtract" => 0x6D,
            "decimal" => 0x6E,
            "divide" => 0x6F,
            "oem_1" or ";" or "semicolon" => 0xBA,
            "oem_plus" or "=" or "equals" => 0xBB,
            "oem_comma" or "," or "comma" => 0xBC,
            "oem_minus" or "-" or "minus" => 0xBD,
            "oem_period" or "." or "period" => 0xBE,
            "oem_2" or "/" or "slash" => 0xBF,
            "oem_3" or "`" or "grave" or "tilde" => 0xC0,
            "oem_4" or "[" or "openbracket" => 0xDB,
            "oem_5" or "\\" or "backslash" => 0xDC,
            "oem_6" or "]" or "closebracket" => 0xDD,
            "oem_7" or "'" or "quote" => 0xDE,
            _ => 0
        };
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        UnregisterAll();
        _source?.RemoveHook(HwndHook);
        Log.Debug("HotkeyManager disposed");
    }
}

