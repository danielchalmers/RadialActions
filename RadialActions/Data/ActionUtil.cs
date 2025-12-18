using System.Runtime.InteropServices;

namespace RadialActions;

/// <summary>
/// Utility class for simulating keyboard input and other system actions.
/// </summary>
public static class ActionUtil
{
    #region Virtual Key Codes

    // Media Keys
    public const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
    public const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    public const byte VK_MEDIA_PREV_TRACK = 0xB1;
    public const byte VK_MEDIA_STOP = 0xB2;

    // Volume Keys
    public const byte VK_VOLUME_MUTE = 0xAD;
    public const byte VK_VOLUME_DOWN = 0xAE;
    public const byte VK_VOLUME_UP = 0xAF;

    // Modifier Keys
    public const byte VK_CONTROL = 0x11;
    public const byte VK_MENU = 0x12; // Alt
    public const byte VK_SHIFT = 0x10;
    public const byte VK_LWIN = 0x5B;

    #endregion

    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

    /// <summary>
    /// Simulates a key press and release.
    /// </summary>
    /// <param name="vk">The virtual key code to simulate.</param>
    public static void SimulateKey(byte vk)
    {
        keybd_event(vk, 0, 0, IntPtr.Zero);
        keybd_event(vk, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
    }

    /// <summary>
    /// Simulates pressing a key down.
    /// </summary>
    public static void KeyDown(byte vk)
    {
        keybd_event(vk, 0, 0, IntPtr.Zero);
    }

    /// <summary>
    /// Simulates releasing a key.
    /// </summary>
    public static void KeyUp(byte vk)
    {
        keybd_event(vk, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
    }

    /// <summary>
    /// Simulates a keyboard shortcut like "Ctrl+C" or "Alt+F4".
    /// </summary>
    /// <param name="shortcut">The shortcut string (e.g., "Ctrl+C", "Alt+Shift+Tab").</param>
    public static void SimulateKeyboardShortcut(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
            return;

        var parts = shortcut.ToLower().Split('+');
        var modifiers = new List<byte>();
        byte mainKey = 0;

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            switch (trimmed)
            {
                case "ctrl":
                case "control":
                    modifiers.Add(VK_CONTROL);
                    break;
                case "alt":
                    modifiers.Add(VK_MENU);
                    break;
                case "shift":
                    modifiers.Add(VK_SHIFT);
                    break;
                case "win":
                case "windows":
                    modifiers.Add(VK_LWIN);
                    break;
                default:
                    mainKey = ParseKeyCode(trimmed);
                    break;
            }
        }

        // Press modifiers
        foreach (var mod in modifiers)
            KeyDown(mod);

        // Press and release main key
        if (mainKey != 0)
            SimulateKey(mainKey);

        // Release modifiers in reverse order
        for (var i = modifiers.Count - 1; i >= 0; i--)
            KeyUp(modifiers[i]);
    }

    /// <summary>
    /// Parses a key name to its virtual key code.
    /// </summary>
    private static byte ParseKeyCode(string keyName)
    {
        // Handle function keys
        if (keyName.StartsWith("f") && int.TryParse(keyName.Substring(1), out var fNum) && fNum >= 1 && fNum <= 24)
            return (byte)(0x6F + fNum); // VK_F1 = 0x70

        // Handle single characters
        if (keyName.Length == 1)
        {
            var c = char.ToUpper(keyName[0]);
            if (c >= 'A' && c <= 'Z')
                return (byte)c;
            if (c >= '0' && c <= '9')
                return (byte)c;
        }

        // Handle special keys
        return keyName switch
        {
            "enter" or "return" => 0x0D,
            "tab" => 0x09,
            "space" => 0x20,
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
            _ => 0
        };
    }
}
