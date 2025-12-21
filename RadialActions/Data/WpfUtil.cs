using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RadialActions;

/// <summary>
/// Utility methods for WPF window positioning.
/// </summary>
public static class WpfUtil
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    /// <summary>
    /// Centers the window on the cursor position, keeping it within screen bounds.
    /// </summary>
    public static void CenterOnCursor(this Window window)
    {
        if (!GetCursorPos(out var cursorPosition))
            return;

        // Get the monitor at cursor position
        var monitor = MonitorFromPoint(cursorPosition, MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

        if (!GetMonitorInfo(monitor, ref monitorInfo))
            return;

        var workArea = monitorInfo.rcWork;
        var windowWidth = (int)window.Width;
        var windowHeight = (int)window.Height;

        // Calculate centered position
        var newLeft = cursorPosition.X - (windowWidth / 2);
        var newTop = cursorPosition.Y - (windowHeight / 2);

        // Clamp to work area bounds
        newLeft = Math.Max(workArea.Left, Math.Min(newLeft, workArea.Right - windowWidth));
        newTop = Math.Max(workArea.Top, Math.Min(newTop, workArea.Bottom - windowHeight));

        // Move the window
        SetWindowPos(new WindowInteropHelper(window).Handle,
                     IntPtr.Zero,
                     newLeft,
                     newTop,
                     0, 0,
                     SWP_NOZORDER | SWP_NOSIZE);
    }

    /// <summary>
    /// Centers the window on the primary screen.
    /// </summary>
    public static void CenterOnScreen(this Window window)
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        window.Left = (screenWidth - window.Width) / 2;
        window.Top = (screenHeight - window.Height) / 2;
    }
}
