namespace RadialActions.Properties;

public sealed partial class Settings
{
    /// <summary>
    /// The index of the selected tab in the settings window.
    /// </summary>
    public int SettingsTabIndex { get; set; }

    /// <summary>
    /// The hotkey used to activate the radial.
    /// </summary>
    public string ActivationHotkey { get; set; } = "ctrl+alt+4";

    /// <summary>
    /// The width and height of the radial.
    /// </summary>
    public int Size { get; set; } = 400;

    /// <summary>
    /// Starts the app in the background when you log in.
    /// </summary>
    public bool RunOnStartup { get; set; } = false;
}
