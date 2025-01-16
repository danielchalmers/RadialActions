using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RadialActions;

public static class WpfUtil
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOSIZE = 0x0001;

    public static void CenterOnCursor(this Window window)
    {
        // Get the cursor position in screen coordinates
        if (GetCursorPos(out var cursorPosition))
        {
            // Calculate new window position
            var newLeft = cursorPosition.X - (window.Width / 2);
            var newTop = cursorPosition.Y - (window.Height / 2);

            // Move the window
            SetWindowPos(new WindowInteropHelper(window).Handle,
                         IntPtr.Zero,
                         (int)newLeft,
                         (int)newTop,
                         0, 0,
                         SWP_NOZORDER | SWP_NOSIZE);
        }
    }

    public static void CenterOnScreen(this Window window)
    {
        // Get the dimensions of the primary screen
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        // Calculate the top-left corner position for the window
        var windowLeft = (screenWidth - window.Width) / 2;
        var windowTop = (screenHeight - window.Height) / 2;

        // Set the window position
        window.Left = windowLeft;
        window.Top = windowTop;
    }
}
