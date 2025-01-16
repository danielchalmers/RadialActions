using System.Runtime.InteropServices;

namespace RadialActions;

public static class PieActions
{
    // Import SendInput from user32.dll
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray)] INPUT[] pInputs, int cbSize);

    // Define the INPUT structure
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
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

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static void SimulateKey(ushort vk)
    {
        // Create key down event
        var inputs = new INPUT[2];

        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki = new KEYBDINPUT
        {
            wVk = vk,
            dwFlags = 0
        };

        // Create key up event
        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].u.ki = new KEYBDINPUT
        {
            wVk = vk,
            dwFlags = KEYEVENTF_KEYUP
        };

        // Send the input events
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }
}
