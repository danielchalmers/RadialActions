using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace RadialActions.Properties;

public sealed partial class Settings
{
    /// <summary>
    /// The index of the selected tab in the settings window.
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private int _settingsTabIndex;

    /// <summary>
    /// The hotkey used to activate the radial menu.
    /// </summary>
    [ObservableProperty]
    private string _activationHotkey = "Ctrl+Alt+Space";

    /// <summary>
    /// The width and height of the radial menu in pixels.
    /// </summary>
    [ObservableProperty]
    private int _size = 400;

    /// <summary>
    /// Starts the app in the background when you log in.
    /// </summary>
    [ObservableProperty]
    private bool _runOnStartup;

    /// <summary>
    /// The collection of actions displayed in the pie menu.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<PieAction> _actions = CreateDefaultActions();

    /// <summary>
    /// The background color of the pie slices (hex format).
    /// </summary>
    [ObservableProperty]
    private string _sliceColor = "#2D2D30";

    /// <summary>
    /// The hover color of the pie slices (hex format).
    /// </summary>
    [ObservableProperty]
    private string _sliceHoverColor = "#3E3E42";

    /// <summary>
    /// The border color of the pie slices (hex format).
    /// </summary>
    [ObservableProperty]
    private string _sliceBorderColor = "#1E1E1E";

    /// <summary>
    /// The text color for slice labels (hex format).
    /// </summary>
    [ObservableProperty]
    private string _textColor = "#FFFFFF";

    /// <summary>
    /// Whether to show the center hole in the pie menu.
    /// </summary>
    [ObservableProperty]
    private bool _showCenterHole = true;

    /// <summary>
    /// The radius of the center hole as a percentage of the total radius (0.0 to 0.5).
    /// </summary>
    [ObservableProperty]
    private double _centerHoleRatio = 0.25;

    /// <summary>
    /// Whether to show icons in the pie slices.
    /// </summary>
    [ObservableProperty]
    private bool _showIcons = true;

    /// <summary>
    /// Whether to show text labels in the pie slices.
    /// </summary>
    [ObservableProperty]
    private bool _showLabels = true;

    /// <summary>
    /// Creates the default set of actions for a new installation.
    /// </summary>
    public static ObservableCollection<PieAction> CreateDefaultActions()
    {
        return new ObservableCollection<PieAction>
        {
            PieAction.CreateKeyAction("PlayPause"),
            PieAction.CreateKeyAction("PreviousTrack"),
            PieAction.CreateKeyAction("NextTrack"),
            PieAction.CreateKeyAction("Mute"),
            PieAction.CreateKeyAction("VolumeDown"),
            PieAction.CreateKeyAction("VolumeUp"),
        };
    }
}
