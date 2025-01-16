using System.Windows;
using System.Windows.Interop;

namespace RadialActions;

internal static class WpfUtil
{
    public static void CenterOnCursor(this Window window)
    {
        // Get the cursor position in screen coordinates
        if (NativeMethods.GetCursorPos(out var cursorPosition))
        {
            // Calculate new window position
            var newLeft = cursorPosition.X - (window.Width / 2);
            var newTop = cursorPosition.Y - (window.Height / 2);

            // Move the window
            NativeMethods.SetWindowPos(new WindowInteropHelper(window).Handle,
                         IntPtr.Zero,
                         (int)newLeft,
                         (int)newTop,
                         0, 0,
                         NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOSIZE);
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
