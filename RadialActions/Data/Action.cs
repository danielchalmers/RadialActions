using System.ComponentModel;
using System.Diagnostics;

namespace RadialActions;

/// <summary>
/// Defines the type of action to perform when a slice is clicked.
/// </summary>
public enum ActionType
{
    /// <summary>No action.</summary>
    None,

    /// <summary>Simulate a media key press (play/pause, next, previous, etc.).</summary>
    MediaKey,

    /// <summary>Simulate a volume control key press.</summary>
    VolumeKey,

    /// <summary>Launch an application or open a file.</summary>
    LaunchApp,

    /// <summary>Open a URL in the default browser.</summary>
    OpenUrl,

    /// <summary>Run a custom command.</summary>
    RunCommand,

    /// <summary>Simulate a keyboard shortcut.</summary>
    Keyboard,
}

/// <summary>
/// Predefined media key types.
/// </summary>
public enum MediaKeyType
{
    PlayPause,
    NextTrack,
    PreviousTrack,
    Stop,
}

/// <summary>
/// Predefined volume key types.
/// </summary>
public enum VolumeKeyType
{
    VolumeUp,
    VolumeDown,
    Mute,
}

/// <summary>
/// Represents an action that can be assigned to a pie slice.
/// </summary>
public class PieAction : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// The display name of the action.
    /// </summary>
    public string Name { get; set; } = "New Action";

    /// <summary>
    /// The icon character or emoji to display.
    /// </summary>
    public string Icon { get; set; } = "⚡";

    /// <summary>
    /// The type of action to perform.
    /// </summary>
    public ActionType Type { get; set; } = ActionType.None;

    /// <summary>
    /// The parameter for the action (path, URL, command, key name, etc.).
    /// </summary>
    public string Parameter { get; set; } = string.Empty;

    /// <summary>
    /// Additional arguments for LaunchApp or RunCommand actions.
    /// </summary>
    public string Arguments { get; set; } = string.Empty;

    /// <summary>
    /// Creates a new empty action.
    /// </summary>
    public PieAction() { }

    /// <summary>
    /// Creates a new action with the specified name and icon.
    /// </summary>
    public PieAction(string name, string icon = "⚡")
    {
        Name = name;
        Icon = icon;
    }

    /// <summary>
    /// Creates a media key action.
    /// </summary>
    public static PieAction CreateMediaAction(MediaKeyType keyType) => keyType switch
    {
        MediaKeyType.PlayPause => new PieAction("Play/Pause", "⏯️") { Type = ActionType.MediaKey, Parameter = "PlayPause" },
        MediaKeyType.NextTrack => new PieAction("Next", "⏭️") { Type = ActionType.MediaKey, Parameter = "NextTrack" },
        MediaKeyType.PreviousTrack => new PieAction("Previous", "⏮️") { Type = ActionType.MediaKey, Parameter = "PreviousTrack" },
        MediaKeyType.Stop => new PieAction("Stop", "⏹️") { Type = ActionType.MediaKey, Parameter = "Stop" },
        _ => new PieAction("Unknown", "❓")
    };

    /// <summary>
    /// Creates a volume control action.
    /// </summary>
    public static PieAction CreateVolumeAction(VolumeKeyType keyType) => keyType switch
    {
        VolumeKeyType.VolumeUp => new PieAction("Volume Up", "🔊") { Type = ActionType.VolumeKey, Parameter = "VolumeUp" },
        VolumeKeyType.VolumeDown => new PieAction("Volume Down", "🔉") { Type = ActionType.VolumeKey, Parameter = "VolumeDown" },
        VolumeKeyType.Mute => new PieAction("Mute", "🔇") { Type = ActionType.VolumeKey, Parameter = "Mute" },
        _ => new PieAction("Unknown", "❓")
    };

    /// <summary>
    /// Creates an app launcher action.
    /// </summary>
    public static PieAction CreateAppAction(string name, string path, string icon = "📁", string arguments = "")
        => new(name, icon) { Type = ActionType.LaunchApp, Parameter = path, Arguments = arguments };

    /// <summary>
    /// Creates a URL opener action.
    /// </summary>
    public static PieAction CreateUrlAction(string name, string url, string icon = "🌐")
        => new(name, icon) { Type = ActionType.OpenUrl, Parameter = url };

    /// <summary>
    /// Creates a command runner action.
    /// </summary>
    public static PieAction CreateCommandAction(string name, string command, string icon = "⚙️", string arguments = "")
        => new(name, icon) { Type = ActionType.RunCommand, Parameter = command, Arguments = arguments };

    /// <summary>
    /// Creates a keyboard shortcut action.
    /// </summary>
    public static PieAction CreateKeyboardAction(string name, string shortcut, string icon = "⌨️")
        => new(name, icon) { Type = ActionType.Keyboard, Parameter = shortcut };

    /// <summary>
    /// Executes the action.
    /// </summary>
    public void Execute()
    {
        Log.Information($"Executing action: {Name} ({Type})");

        try
        {
            switch (Type)
            {
                case ActionType.MediaKey:
                    ExecuteMediaKey();
                    break;

                case ActionType.VolumeKey:
                    ExecuteVolumeKey();
                    break;

                case ActionType.LaunchApp:
                    ExecuteLaunchApp();
                    break;

                case ActionType.OpenUrl:
                    ExecuteOpenUrl();
                    break;

                case ActionType.RunCommand:
                    ExecuteRunCommand();
                    break;

                case ActionType.Keyboard:
                    ExecuteKeyboard();
                    break;

                case ActionType.None:
                default:
                    Log.Warning("Action has no operation defined");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to execute action: {Name}");
        }
    }

    private void ExecuteMediaKey()
    {
        var vk = Parameter switch
        {
            "PlayPause" => ActionUtil.VK_MEDIA_PLAY_PAUSE,
            "NextTrack" => ActionUtil.VK_MEDIA_NEXT_TRACK,
            "PreviousTrack" => ActionUtil.VK_MEDIA_PREV_TRACK,
            "Stop" => ActionUtil.VK_MEDIA_STOP,
            _ => (byte)0
        };

        if (vk != 0)
            ActionUtil.SimulateKey(vk);
    }

    private void ExecuteVolumeKey()
    {
        var vk = Parameter switch
        {
            "VolumeUp" => ActionUtil.VK_VOLUME_UP,
            "VolumeDown" => ActionUtil.VK_VOLUME_DOWN,
            "Mute" => ActionUtil.VK_VOLUME_MUTE,
            _ => (byte)0
        };

        if (vk != 0)
            ActionUtil.SimulateKey(vk);
    }

    private void ExecuteLaunchApp()
    {
        if (string.IsNullOrWhiteSpace(Parameter))
            return;

        var psi = new ProcessStartInfo(Parameter)
        {
            UseShellExecute = true,
            Arguments = Arguments ?? string.Empty
        };
        Process.Start(psi);
    }

    private void ExecuteOpenUrl()
    {
        if (string.IsNullOrWhiteSpace(Parameter))
            return;

        Process.Start(new ProcessStartInfo(Parameter) { UseShellExecute = true });
    }

    private void ExecuteRunCommand()
    {
        if (string.IsNullOrWhiteSpace(Parameter))
            return;

        var psi = new ProcessStartInfo("cmd.exe", $"/c {Parameter} {Arguments}")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        Process.Start(psi);
    }

    private void ExecuteKeyboard()
    {
        if (string.IsNullOrWhiteSpace(Parameter))
            return;

        ActionUtil.SimulateKeyboardShortcut(Parameter);
    }

    public override string ToString() => Name;
}
