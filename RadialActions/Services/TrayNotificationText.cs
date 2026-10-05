namespace RadialActions;

/// <summary>
/// A tray notification's title and message.
/// </summary>
internal sealed record TrayNotification(string Title, string Message);

/// <summary>
/// Copy for the tray icon's notifications and tooltip.
/// </summary>
internal static class TrayNotificationText
{
    private const string AppName = "Radial Actions";

    /// <summary>
    /// First-run notification that says how to open the menu and, while the app doesn't start at sign-in, how to make it.
    /// </summary>
    /// <param name="hotkey">The registered activation hotkey, or null or empty when there isn't one.</param>
    /// <param name="runsAtStartup">Whether Run at Windows startup is on; without it the hotkey stops working after a restart.</param>
    public static TrayNotification Welcome(string hotkey, bool runsAtStartup)
    {
        var howToOpen = string.IsNullOrWhiteSpace(hotkey)
            ? $"Select the {AppName} tray icon to open the menu."
            : $"Press {hotkey.Trim()} to open the menu.";

        // Selecting the welcome opens the General tab, where the setting is.
        return new(
            $"{AppName} is running",
            runsAtStartup ? howToOpen : $"{howToOpen} To start {AppName} when you sign in, select this and turn on Run at Windows startup.");
    }

    /// <summary>
    /// Shown at startup while the activation hotkey can't be registered; selecting it opens Settings on the hotkey.
    /// </summary>
    /// <param name="problem">The reason from <see cref="ActivationHotkeyStatus.GetProblem"/>.</param>
    public static TrayNotification HotkeyUnavailable(string problem) => new(
        "Hotkey unavailable",
        $"{problem} Select to change it.");

    /// <summary>
    /// Picks the notification to show after the first hotkey registration at startup: a hotkey problem on every launch until it's fixed, otherwise the welcome once.
    /// </summary>
    /// <param name="hotkey">The saved activation hotkey.</param>
    /// <param name="hasShownWelcome">Whether the welcome appeared on an earlier launch.</param>
    /// <param name="runsAtStartup">Whether Run at Windows startup is on, which the welcome mentions while it's off.</param>
    /// <param name="isWelcome">True when the result is the welcome, which the caller records as shown so it doesn't appear again.</param>
    /// <returns>The notification, or null when there's nothing to say.</returns>
    public static TrayNotification ForStartup(HotkeyRegistrationResult result, string hotkey, bool hasShownWelcome, bool runsAtStartup, out bool isWelcome)
    {
        isWelcome = false;

        // The menu can't open from the keyboard, so this repeats on every launch rather than once.
        if (result is HotkeyRegistrationResult.Unrecognized or HotkeyRegistrationResult.Failed)
        {
            return HotkeyUnavailable(ActivationHotkeyStatus.GetProblem(result, hotkey));
        }

        if (hasShownWelcome)
        {
            return null;
        }

        isWelcome = true;
        return Welcome(result == HotkeyRegistrationResult.Registered ? hotkey : null, runsAtStartup);
    }

    public static TrayNotification UpdateAvailable(Version latestVersion) => new(
        "Update available",
        $"Version {AppInfoFormatter.FormatVersion(latestVersion)} is ready to download. Select to see what's new.");

    /// <summary>
    /// The tray icon's tooltip, which names the hotkey only while it works.
    /// </summary>
    public static string ToolTip(string registeredHotkey) =>
        string.IsNullOrWhiteSpace(registeredHotkey) ? AppName : $"{AppName} ({registeredHotkey.Trim()})";
}
