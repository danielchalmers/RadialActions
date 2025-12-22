using System.Runtime.InteropServices;
using System.Windows.Input;
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
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private int _currentId;
    private readonly Dictionary<string, int> _hotkeys = new(StringComparer.OrdinalIgnoreCase);
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
            _hotkeys.Remove(hotkey);
            Log.Debug($"Unregistered existing hotkey: {hotkey}");
        }

        if (!HotkeyUtil.TryParse(hotkey, out var modifiers, out var key))
        {
            Log.Warning($"Could not parse hotkey: {hotkey}");
            return false;
        }

        var keyCode = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (keyCode == 0)
        {
            Log.Warning($"Could not resolve key code for hotkey: {hotkey}");
            return false;
        }

        var id = ++_currentId;

        if (RegisterHotKey(_windowHandle, id, ToModifierFlags(modifiers), keyCode))
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

    private static uint ToModifierFlags(ModifierKeys modifiers)
    {
        uint flags = 0;
        if ((modifiers & ModifierKeys.Alt) != 0)
            flags |= ModAlt;
        if ((modifiers & ModifierKeys.Control) != 0)
            flags |= ModControl;
        if ((modifiers & ModifierKeys.Shift) != 0)
            flags |= ModShift;
        if ((modifiers & ModifierKeys.Windows) != 0)
            flags |= ModWin;

        return flags;
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

