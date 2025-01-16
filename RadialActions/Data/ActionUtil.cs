using System.Runtime.InteropServices;

namespace RadialActions;

public static class ActionUtil
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static void SimulateKey(byte vk)
    {
        // Press the Play/Pause key
        keybd_event(vk, 0, 0, IntPtr.Zero);

        // Release the Play/Pause key
        keybd_event(vk, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
    }
}
