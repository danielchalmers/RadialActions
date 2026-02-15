using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace RadialActions.Properties;

public sealed partial class Settings
{
    public const string DefaultActivationHotkey = "Ctrl+Alt+Space";
    public const int DefaultSize = 400;

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
    private string _activationHotkey = DefaultActivationHotkey;

    /// <summary>
    /// The width and height of the radial menu in pixels.
    /// </summary>
    [ObservableProperty]
    private int _size = DefaultSize;

    /// <summary>
    /// Starts the app in the background when you log in.
    /// </summary>
    [ObservableProperty]
    private bool _runOnStartup;

    /// <summary>
    /// Keeps the menu open after clicking a slice until focus is lost.
    /// </summary>
    [ObservableProperty]
    private bool _keepMenuOpenAfterSliceClick;

    /// <summary>
    /// The collection of actions displayed in the pie menu.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<PieAction> _actions = CreateDefaultActions();

    /// <summary>
    /// Creates the default set of actions for a new installation.
    /// </summary>
    public static ObservableCollection<PieAction> CreateDefaultActions()
    {
        return
        [
            PieAction.CreateKeyAction("PlayPause"),
            PieAction.CreateKeyAction("PreviousTrack"),
            PieAction.CreateKeyAction("NextTrack"),
            PieAction.CreateKeyAction("Mute"),
            PieAction.CreateShellAction("File Explorer", "explorer.exe", "📁"),
        ];
    }

    internal void NormalizeAfterLoad()
    {
        ActivationHotkey ??= DefaultActivationHotkey;
        if (Size <= 0)
        {
            Size = DefaultSize;
        }

        if (Actions == null)
        {
            Actions = CreateDefaultActions();
            return;
        }

        for (var i = Actions.Count - 1; i >= 0; i--)
        {
            if (Actions[i] == null)
            {
                Actions.RemoveAt(i);
            }
        }

        foreach (var action in Actions)
        {
            action.Name = string.IsNullOrWhiteSpace(action.Name) ? PieAction.DefaultName : action.Name;
            action.Icon = string.IsNullOrWhiteSpace(action.Icon) ? PieAction.DefaultIcon : action.Icon;
            action.Parameter ??= string.Empty;
            action.Arguments ??= string.Empty;
            action.WorkingDirectory ??= string.Empty;

            if (!Enum.IsDefined(typeof(ActionType), action.Type))
            {
                action.Type = ActionType.None;
            }
        }
    }
}
