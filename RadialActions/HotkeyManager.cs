﻿using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace RadialActions;

public class HotkeyManager
{
    private readonly IntPtr _windowHandle;
    private readonly HwndSource _source;
    private const int WM_HOTKEY = 0x0312;
    private static int _currentId = 0; // Counter for generating unique IDs
    private readonly ConcurrentDictionary<string, int> _hotkeys = new();

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public event EventHandler HotkeyPressed;

    public HotkeyManager(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(HwndHook);
    }

    public bool RegisterHotkey(string hotkey)
    {
        if (_hotkeys.ContainsKey(hotkey))
        {
            var unregistered = UnregisterHotkey(hotkey);

            if (unregistered)
            {
                Log.Debug($"Hotkey unregistered <{hotkey}>");
            }
            else
            {
                Log.Debug($"Hotkey failed to unregister <{hotkey}>");
            }
        }

        var (modifiers, keyCode) = ParseHotkey(hotkey);
        var id = ++_currentId;

        var registered = RegisterHotKey(_windowHandle, id, modifiers, keyCode);

        if (registered)
        {
            Log.Information($"Hotkey registered <{hotkey}>");
            return true;
        }
        else
        {
            Log.Information($"Hotkey failed to register <{hotkey}>");
            return false;
        }
    }

    public bool UnregisterHotkey(string hotkey)
    {
        var id = _hotkeys[hotkey];
        return UnregisterHotKey(_windowHandle, id);
    }

    private (uint modifiers, uint keyCode) ParseHotkey(string hotkey)
    {
        uint modifiers = 0;
        uint keyCode = 0;

        var parts = hotkey.ToLower().Split('+');
        foreach (var part in parts)
        {
            switch (part.Trim())
            {
                case "ctrl":
                    modifiers |= 0x0002;
                    break;
                case "alt":
                    modifiers |= 0x0001;
                    break;
                case "shift":
                    modifiers |= 0x0004;
                    break;
                case "win":
                    modifiers |= 0x0008;
                    break;
                default:
                    keyCode = part.ToUpper()[0];
                    break;
            }
        }

        return (modifiers, keyCode);
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
}
