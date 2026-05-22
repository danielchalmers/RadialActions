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

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);

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

    private enum MonitorDpiType
    {
        Effective = 0,
    }

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int DefaultDpi = 96;
    private const uint MaxSupportedDpi = 10_000;

    /// <summary>
    /// Centers the window on the cursor position, keeping it within screen bounds.
    /// </summary>
    public static void CenterOnCursor(this Window window)
    {
        if (!GetCursorPos(out var cursorPosition))
            return;

        if (!TryGetMonitorInfo(cursorPosition, out var monitor, out var monitorInfo))
            return;

        var position = CalculateCenteredPositionInDevicePixels(
            new Point(cursorPosition.X, cursorPosition.Y),
            ToRect(monitorInfo.rcWork),
            GetWindowSize(window),
            GetDpiScale(monitor));

        SetWindowPos(new WindowInteropHelper(window).Handle,
                     IntPtr.Zero,
                     (int)position.X,
                     (int)position.Y,
                     0, 0,
                     SWP_NOZORDER | SWP_NOSIZE);
    }

    /// <summary>
    /// Centers the window on the primary screen.
    /// </summary>
    public static void CenterOnScreen(this Window window)
    {
        if (!GetCursorPos(out var cursorPosition) || !TryGetMonitorInfo(cursorPosition, out var monitor, out var monitorInfo))
        {
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            var windowSize = GetWindowSize(window);
            window.Left = (screenWidth - windowSize.Width) / 2;
            window.Top = (screenHeight - windowSize.Height) / 2;
            return;
        }

        var screenBounds = ToRect(monitorInfo.rcMonitor);
        var screenCenter = new Point(screenBounds.Left + (screenBounds.Width / 2), screenBounds.Top + (screenBounds.Height / 2));
        var position = CalculateCenteredPositionInDevicePixels(
            screenCenter,
            screenBounds,
            GetWindowSize(window),
            GetDpiScale(monitor));

        SetWindowPos(new WindowInteropHelper(window).Handle,
                     IntPtr.Zero,
                     (int)position.X,
                     (int)position.Y,
                     0, 0,
                     SWP_NOZORDER | SWP_NOSIZE);
    }

    internal static Point CalculateCenteredPositionInDevicePixels(Point targetPosition, Rect bounds, Size windowSize, Point dpiScale)
    {
        var windowWidth = ScaleDipToPixels(windowSize.Width, dpiScale.X);
        var windowHeight = ScaleDipToPixels(windowSize.Height, dpiScale.Y);

        var newLeft = (int)Math.Round(targetPosition.X - (windowWidth / 2.0));
        var newTop = (int)Math.Round(targetPosition.Y - (windowHeight / 2.0));

        var minLeft = (int)Math.Round(bounds.Left);
        var minTop = (int)Math.Round(bounds.Top);
        var maxLeft = (int)Math.Round(bounds.Right) - windowWidth;
        var maxTop = (int)Math.Round(bounds.Bottom) - windowHeight;

        newLeft = Math.Max(minLeft, Math.Min(newLeft, maxLeft));
        newTop = Math.Max(minTop, Math.Min(newTop, maxTop));

        return new Point(newLeft, newTop);
    }

    internal static int ScaleDipToPixels(double dipValue, double dpiScale)
    {
        if (dipValue <= 0 || dpiScale <= 0)
        {
            return 0;
        }

        return (int)Math.Round(dipValue * dpiScale);
    }

    private static bool TryGetMonitorInfo(POINT point, out IntPtr monitor, out MONITORINFO monitorInfo)
    {
        monitor = MonitorFromPoint(point, MONITOR_DEFAULTTONEAREST);
        monitorInfo = new MONITORINFO();
        monitorInfo.cbSize = Marshal.SizeOf<MONITORINFO>();
        return monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref monitorInfo);
    }

    private static Size GetWindowSize(Window window)
    {
        var width = window.ActualWidth;
        var height = window.ActualHeight;

        if (width > 0 && height > 0)
        {
            return new Size(width, height);
        }

        width = !double.IsNaN(window.Width) ? window.Width : window.RenderSize.Width;
        height = !double.IsNaN(window.Height) ? window.Height : window.RenderSize.Height;
        return new Size(Math.Max(0, width), Math.Max(0, height));
    }

    private static Point GetDpiScale(IntPtr monitor)
    {
        try
        {
            var result = GetDpiForMonitor(monitor, MonitorDpiType.Effective, out var dpiX, out var dpiY);
            if (result == 0 && dpiX > 0 && dpiY > 0 && dpiX <= MaxSupportedDpi && dpiY <= MaxSupportedDpi)
            {
                return new Point(dpiX / (double)DefaultDpi, dpiY / (double)DefaultDpi);
            }
        }
        catch (DllNotFoundException)
        {
            // Older Windows versions may not expose shcore; fall back to 100% scaling.
        }
        catch (EntryPointNotFoundException)
        {
            // If monitor DPI APIs are unavailable, use default DPI so positioning still works.
        }

        return new Point(1, 1);
    }

    private static Rect ToRect(RECT rect)
    {
        return new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }
}
