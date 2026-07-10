using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace RadialActions;

/// <summary>
/// Executes built-in system actions like locking the workstation or toggling dark mode.
/// </summary>
public static class SystemActionUtil
{
    private const uint SHERB_NOCONFIRMATION = 0x00000001;

    private const int HWND_BROADCAST = 0xFFFF;
    private const int WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool bHibernate, bool bForce, bool bWakeupEventsDisabled);

    [DllImport("shell32.dll")]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    /// <summary>
    /// Locks the workstation, same as Win+L.
    /// </summary>
    public static void LockWorkstation()
    {
        if (!LockWorkStation())
            throw new InvalidOperationException("Could not lock the workstation");
    }

    /// <summary>
    /// Puts the computer to sleep.
    /// </summary>
    public static void Sleep()
    {
        if (!SetSuspendState(false, false, false))
            throw new InvalidOperationException("Could not put the computer to sleep");
    }

    /// <summary>
    /// Minimizes all windows to show the desktop, same as Win+D.
    /// </summary>
    public static void ShowDesktop() => ActionUtil.SimulateKeyboardShortcut("Win+D");

    /// <summary>
    /// Opens the snipping overlay to capture a screenshot, same as Win+Shift+S.
    /// </summary>
    public static void Screenshot() => ActionUtil.SimulateKeyboardShortcut("Win+Shift+S");

    /// <summary>
    /// Empties the recycle bin without a confirmation prompt.
    /// </summary>
    public static void EmptyRecycleBin()
    {
        // The result is ignored because it reports an error for benign cases like the bin already being empty.
        _ = SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION);
    }

    /// <summary>
    /// Toggles Windows between light and dark mode for apps and the system.
    /// </summary>
    public static void ToggleDarkMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath, writable: true) ??
            throw new InvalidOperationException("Could not open the personalization settings");

        var useLightTheme = key.GetValue("AppsUseLightTheme") as int? ?? 1;
        var newValue = useLightTheme == 0 ? 1 : 0;
        key.SetValue("AppsUseLightTheme", newValue, RegistryValueKind.DWord);
        key.SetValue("SystemUsesLightTheme", newValue, RegistryValueKind.DWord);

        // Tells running apps to re-read the theme so the change applies immediately.
        SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, "ImmersiveColorSet", SMTO_ABORTIFHUNG, 1000, out _);
    }
}
