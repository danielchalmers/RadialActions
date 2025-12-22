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
    private static void KeyDown(byte vk)
    {
        keybd_event(vk, 0, 0, IntPtr.Zero);
    }

    /// <summary>
    /// Simulates releasing a key.
    /// </summary>
    private static void KeyUp(byte vk)
    {
        keybd_event(vk, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
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
