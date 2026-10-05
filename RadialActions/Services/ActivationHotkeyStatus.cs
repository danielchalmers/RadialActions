namespace RadialActions;

/// <summary>
/// Explains why the activation hotkey can't open the menu, for the Settings status line and the startup notification.
/// </summary>
internal static class ActivationHotkeyStatus
{
    /// <summary>
    /// What's wrong with the hotkey, in one or two sentences, or null when it registered.
    /// </summary>
    public static string GetProblem(HotkeyRegistrationResult result, string hotkey) => result switch
    {
        HotkeyRegistrationResult.Registered => null,
        HotkeyRegistrationResult.Empty => "No hotkey is set.",
        HotkeyRegistrationResult.Unrecognized => $"Radial Actions doesn't recognize \"{hotkey?.Trim()}\".",
        _ => $"Couldn't register {hotkey?.Trim()}. Another app may be using it.",
    };

    /// <summary>
    /// The message shown under the hotkey box in Settings, or null when the hotkey registered.
    /// </summary>
    public static string GetError(HotkeyRegistrationResult result, string hotkey) => result switch
    {
        HotkeyRegistrationResult.Registered => null,
        HotkeyRegistrationResult.Empty => "No hotkey is set. Open the menu from the tray icon, or record a hotkey.",
        _ => $"{GetProblem(result, hotkey)} Record a different combination.",
    };
}
