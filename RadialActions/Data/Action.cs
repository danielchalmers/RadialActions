using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RadialActions;

/// <summary>
/// Defines the type of action to perform when a slice is clicked.
/// </summary>
public enum ActionType
{
    /// <summary>
    /// No action configured yet.
    /// </summary>
    None = 0,

    /// <summary>
    /// Simulate a predefined key (media/volume) or a custom shortcut.
    /// </summary>
    Key = 1,

    /// <summary>
    /// Launch an app, open a file, or open a URL using shell execution.
    /// </summary>
    Shell = 2,
}

/// <summary>
/// Defines a selectable key action.
/// </summary>
public sealed class KeyActionDefinition
{
    public KeyActionDefinition(string id, string name, string icon, byte virtualKey)
    {
        Id = id;
        Name = name;
        Icon = icon;
        VirtualKey = virtualKey;
    }

    public string Id { get; }
    public string Name { get; }
    public string Icon { get; }
    public byte VirtualKey { get; }
}

/// <summary>
/// Represents an action that can be assigned to a pie slice.
/// </summary>
public partial class PieAction : ObservableObject
{
    public const string DefaultName = "New Action";
    public const string DefaultIcon = "⚡";

    private static readonly IReadOnlyList<KeyActionDefinition> _keyActions =
    [
        new("PlayPause", "Play/Pause", "⏯️", ActionUtil.VK_MEDIA_PLAY_PAUSE),
        new("PreviousTrack", "Previous", "⏮️", ActionUtil.VK_MEDIA_PREV_TRACK),
        new("NextTrack", "Next", "⏭️", ActionUtil.VK_MEDIA_NEXT_TRACK),
        new("Stop", "Stop", "⏹️", ActionUtil.VK_MEDIA_STOP),
        new("Mute", "Mute", "🔇", ActionUtil.VK_VOLUME_MUTE),
        new("VolumeDown", "Volume Down", "🔉", ActionUtil.VK_VOLUME_DOWN),
        new("VolumeUp", "Volume Up", "🔊", ActionUtil.VK_VOLUME_UP),
    ];

    private static readonly IReadOnlyDictionary<string, KeyActionDefinition> _keyActionsById =
        _keyActions.ToDictionary(action => action.Id, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<KeyActionDefinition> KeyActions => _keyActions;

    public static bool TryGetKeyAction(string id, out KeyActionDefinition definition)
    {
        definition = null;
        return !string.IsNullOrWhiteSpace(id) && _keyActionsById.TryGetValue(id, out definition);
    }

    /// <summary>
    /// The display name of the action.
    /// </summary>
    [ObservableProperty]
    private string _name = DefaultName;

    /// <summary>
    /// The icon character or emoji to display.
    /// </summary>
    [ObservableProperty]
    private string _icon = DefaultIcon;

    /// <summary>
    /// The type of action to perform.
    /// </summary>
    [ObservableProperty]
    private ActionType _type = ActionType.None;

    /// <summary>
    /// Whether the action is enabled and should appear in the menu.
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled = true;

    /// <summary>
    /// The parameter for the action (path, URL, command, key name, etc.).
    /// </summary>
    [ObservableProperty]
    private string _parameter = string.Empty;

    /// <summary>
    /// Additional arguments for shell actions.
    /// </summary>
    [ObservableProperty]
    private string _arguments = string.Empty;

    /// <summary>
    /// Optional working directory for shell actions.
    /// </summary>
    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    /// <summary>
    /// Creates a new empty action.
    /// </summary>
    public PieAction() { }

    /// <summary>
    /// Creates a new action with the specified name and icon.
    /// </summary>
    public PieAction(string name, string icon = DefaultIcon)
    {
        Name = name;
        Icon = icon;
    }

    /// <summary>
    /// Creates a key action.
    /// </summary>
    public static PieAction CreateKeyAction(string keyActionId)
    {
        if (!TryGetKeyAction(keyActionId, out var definition))
        {
            definition = _keyActions[0];
        }

        return new PieAction(definition.Name, definition.Icon)
        {
            Type = ActionType.Key,
            Parameter = definition.Id,
        };
    }

    /// <summary>
    /// Creates a shell action.
    /// </summary>
    public static PieAction CreateShellAction(string name, string target, string icon = "📁", string arguments = "", string workingDirectory = "")
        => new(name, icon)
        {
            Type = ActionType.Shell,
            Parameter = target,
            Arguments = arguments,
            WorkingDirectory = workingDirectory
        };

    /// <summary>
    /// Executes the action.
    /// </summary>
    public void Execute()
    {
        if (!IsEnabled)
        {
            Log.Debug("Skipping disabled action: {ActionName}", Name);
            return;
        }

        Log.Information($"Executing action: {Name} ({Type})");

        try
        {
            switch (Type)
            {
                case ActionType.None:
                    Log.Warning("Action has no operation defined");
                    break;
                case ActionType.Key:
                    ExecuteKey();
                    break;

                case ActionType.Shell:
                    ExecuteShell();
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to execute action: {Name}");
        }
    }

    private void ExecuteKey()
    {
        if (string.IsNullOrWhiteSpace(Parameter))
            return;

        if (TryGetKeyAction(Parameter, out var definition))
        {
            ActionUtil.SimulateKey(definition.VirtualKey);
            return;
        }

        ActionUtil.SimulateKeyboardShortcut(Parameter);
    }

    private void ExecuteShell()
    {
        if (string.IsNullOrWhiteSpace(Parameter))
            return;

        var psi = new ProcessStartInfo(Parameter)
        {
            UseShellExecute = true,
            Arguments = Arguments ?? string.Empty
        };

        if (!string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            psi.WorkingDirectory = WorkingDirectory;
        }

        Process.Start(psi);
    }

    public override string ToString() => Name;
}
