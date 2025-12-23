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

    private const int WM_APPCOMMAND = 0x0319;
    private const int APPCOMMAND_MEDIA_NEXTTRACK = 11;
    private const int APPCOMMAND_MEDIA_PREVIOUSTRACK = 12;
    private const int APPCOMMAND_MEDIA_STOP = 13;
    private const int APPCOMMAND_MEDIA_PLAY_PAUSE = 14;

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

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

    /// <summary>
    /// Simulates a key press and release.
    /// </summary>
    /// <param name="vk">The virtual key code to simulate.</param>
    public static void SimulateKey(byte vk)
    {
        if (TrySendMediaAppCommand(vk))
            return;

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
            flags |= KEYEVENTF_EXTENDEDKEY;

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

        if (SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()) == 0)
        {
            keybd_event(vk, 0, flags, IntPtr.Zero);
        }
    }

    private static bool IsExtendedKey(byte vk)
    {
        return vk is VK_MEDIA_PLAY_PAUSE
            or VK_MEDIA_NEXT_TRACK
            or VK_MEDIA_PREV_TRACK
            or VK_MEDIA_STOP
            or VK_VOLUME_MUTE
            or VK_VOLUME_DOWN
            or VK_VOLUME_UP;
    }

    private static bool TrySendMediaAppCommand(byte vk)
    {
        var appCommand = vk switch
        {
            VK_MEDIA_PLAY_PAUSE => APPCOMMAND_MEDIA_PLAY_PAUSE,
            VK_MEDIA_NEXT_TRACK => APPCOMMAND_MEDIA_NEXTTRACK,
            VK_MEDIA_PREV_TRACK => APPCOMMAND_MEDIA_PREVIOUSTRACK,
            VK_MEDIA_STOP => APPCOMMAND_MEDIA_STOP,
            _ => -1
        };

        if (appCommand < 0)
            return false;

        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return false;

        var lParam = (IntPtr)(appCommand << 16);
        SendMessage(hwnd, WM_APPCOMMAND, hwnd, lParam);
        return true;
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
