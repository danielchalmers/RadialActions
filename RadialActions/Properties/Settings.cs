using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace RadialActions.Properties;

public sealed partial class Settings
{
    /// <summary>
    /// The index of the selected tab in the settings window.
    /// </summary>
    [JsonIgnore]
    public int SettingsTabIndex { get; set; }

    /// <summary>
    /// The hotkey used to activate the radial menu.
    /// </summary>
    public string ActivationHotkey { get; set; } = "Ctrl+Alt+Space";

    /// <summary>
    /// The width and height of the radial menu in pixels.
    /// </summary>
    public int Size { get; set; } = 400;

    /// <summary>
    /// Starts the app in the background when you log in.
    /// </summary>
    public bool RunOnStartup { get; set; } = false;

    /// <summary>
    /// The collection of actions displayed in the pie menu.
    /// </summary>
    public ObservableCollection<PieAction> Actions { get; set; } = CreateDefaultActions();

    /// <summary>
    /// The background color of the pie slices (hex format).
    /// </summary>
    public string SliceColor { get; set; } = "#2D2D30";

    /// <summary>
    /// The hover color of the pie slices (hex format).
    /// </summary>
    public string SliceHoverColor { get; set; } = "#3E3E42";

    /// <summary>
    /// The border color of the pie slices (hex format).
    /// </summary>
    public string SliceBorderColor { get; set; } = "#1E1E1E";

    /// <summary>
    /// The text color for slice labels (hex format).
    /// </summary>
    public string TextColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// Whether to show the center hole in the pie menu.
    /// </summary>
    public bool ShowCenterHole { get; set; } = true;

    /// <summary>
    /// The radius of the center hole as a percentage of the total radius (0.0 to 0.5).
    /// </summary>
    public double CenterHoleRatio { get; set; } = 0.25;

    /// <summary>
    /// Whether to show icons in the pie slices.
    /// </summary>
    public bool ShowIcons { get; set; } = true;

    /// <summary>
    /// Whether to show text labels in the pie slices.
    /// </summary>
    public bool ShowLabels { get; set; } = true;

    /// <summary>
    /// Creates the default set of actions for a new installation.
    /// </summary>
    public static ObservableCollection<PieAction> CreateDefaultActions()
    {
        return new ObservableCollection<PieAction>
        {
            PieAction.CreateMediaAction(MediaKeyType.PlayPause),
            PieAction.CreateMediaAction(MediaKeyType.PreviousTrack),
            PieAction.CreateMediaAction(MediaKeyType.NextTrack),
            PieAction.CreateVolumeAction(VolumeKeyType.Mute),
            PieAction.CreateVolumeAction(VolumeKeyType.VolumeDown),
            PieAction.CreateVolumeAction(VolumeKeyType.VolumeUp),
        };
    }
}
