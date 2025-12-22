using System.Runtime.InteropServices;
using System.Windows.Input;

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

    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint INPUT_KEYBOARD = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    /// <summary>
    /// Simulates a key press and release.
    /// </summary>
    /// <param name="vk">The virtual key code to simulate.</param>
    public static void SimulateKey(byte vk)
    {
        SendKey(vk, isKeyUp: false);
        SendKey(vk, isKeyUp: true);
    }

    /// <summary>
    /// Simulates pressing a key down.
    /// </summary>
    private static void KeyDown(byte vk)
    {
        SendKey(vk, isKeyUp: false);
    }

    /// <summary>
    /// Simulates releasing a key.
    /// </summary>
    private static void KeyUp(byte vk)
    {
        SendKey(vk, isKeyUp: true);
    }

    private static void SendKey(byte vk, bool isKeyUp)
    {
        var flags = isKeyUp ? KEYEVENTF_KEYUP : 0u;
        if (IsExtendedKey(vk))
        {
            flags |= KEYEVENTF_EXTENDEDKEY;
        }

        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        _ = SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    private static bool IsExtendedKey(byte vk)
    {
        return vk switch
        {
            VK_MEDIA_NEXT_TRACK or VK_MEDIA_PREV_TRACK or VK_MEDIA_STOP or VK_MEDIA_PLAY_PAUSE => true,
            VK_VOLUME_MUTE or VK_VOLUME_DOWN or VK_VOLUME_UP => true,
            VK_LWIN => true,
            0x5C => true, // VK_RWIN
            0x5D => true, // VK_APPS
            0x21 => true, // VK_PRIOR (Page Up)
            0x22 => true, // VK_NEXT (Page Down)
            0x23 => true, // VK_END
            0x24 => true, // VK_HOME
            0x25 => true, // VK_LEFT
            0x26 => true, // VK_UP
            0x27 => true, // VK_RIGHT
            0x28 => true, // VK_DOWN
            0x2C => true, // VK_SNAPSHOT (PrintScreen)
            0x2D => true, // VK_INSERT
            0x2E => true, // VK_DELETE
            0x6F => true, // VK_DIVIDE (Numpad /)
            0x90 => true, // VK_NUMLOCK
            0xA3 => true, // VK_RCONTROL
            0xA5 => true, // VK_RMENU
            _ => false
        };
    }

    /// <summary>
    /// Simulates a keyboard shortcut like "Ctrl+C" or "Alt+F4".
    /// </summary>
    /// <param name="shortcut">The shortcut string (e.g., "Ctrl+C", "Alt+Shift+Tab").</param>
    public static void SimulateKeyboardShortcut(string shortcut)
    {
        if (!HotkeyUtil.TryParse(shortcut, out var modifiers, out var key))
            return;

        var modifierKeys = new List<byte>(4);
        if ((modifiers & ModifierKeys.Control) != 0)
            modifierKeys.Add(VK_CONTROL);
        if ((modifiers & ModifierKeys.Alt) != 0)
            modifierKeys.Add(VK_MENU);
        if ((modifiers & ModifierKeys.Shift) != 0)
            modifierKeys.Add(VK_SHIFT);
        if ((modifiers & ModifierKeys.Windows) != 0)
            modifierKeys.Add(VK_LWIN);

        // Press modifiers
        foreach (var mod in modifierKeys)
            KeyDown(mod);

        // Press and release main key
        var mainKey = KeyInterop.VirtualKeyFromKey(key);
        if (mainKey != 0)
            SimulateKey((byte)mainKey);

        // Release modifiers in reverse order
        for (var i = modifierKeys.Count - 1; i >= 0; i--)
            KeyUp(modifierKeys[i]);
    }
}
