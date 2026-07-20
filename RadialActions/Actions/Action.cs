using System.Diagnostics;
using System.IO;
using System.Text;
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
    /// Open an app, file, folder, or URL using shell execution.
    /// </summary>
    Open = 2,

    /// <summary>
    /// Run an inline PowerShell script, optionally without a console window.
    /// </summary>
    Script = 3,
}

/// <summary>
/// Defines a selectable key action.
/// </summary>
public sealed class KeyActionDefinition
{
    public KeyActionDefinition(string id, string name, string icon, string category, byte virtualKey)
    {
        Id = id;
        Name = name;
        Icon = icon;
        Category = category;
        VirtualKey = virtualKey;
    }

    public string Id { get; }
    public string Name { get; }
    public string Icon { get; }
    public string Category { get; }
    public byte VirtualKey { get; }
}

/// <summary>
/// Represents an action that can be assigned to a pie slice.
/// </summary>
public partial class PieAction : ObservableObject
{
    public const string DefaultName = "New Action";
    public const string DefaultIcon = "⚡";
    public const string DefaultScriptInterpreter = "powershell.exe";

    public const string MediaCategory = "Media";
    public const string VolumeCategory = "Volume";
    public const string SystemCategory = "System";

    private static readonly IReadOnlyList<KeyActionDefinition> _keyActions =
    [
        new("PlayPause", "Play/Pause", "⏯️", MediaCategory, ActionUtil.VK_MEDIA_PLAY_PAUSE),
        new("PreviousTrack", "Previous Track", "⏮️", MediaCategory, ActionUtil.VK_MEDIA_PREV_TRACK),
        new("NextTrack", "Next Track", "⏭️", MediaCategory, ActionUtil.VK_MEDIA_NEXT_TRACK),
        new("Stop", "Stop", "⏹️", MediaCategory, ActionUtil.VK_MEDIA_STOP),

        new("Mute", "Mute", "🔇", VolumeCategory, ActionUtil.VK_VOLUME_MUTE),
        new("VolumeDown", "Volume Down", "🔉", VolumeCategory, ActionUtil.VK_VOLUME_DOWN),
        new("VolumeUp", "Volume Up", "🔊", VolumeCategory, ActionUtil.VK_VOLUME_UP),

        new("PrintScreen", "Print Screen", "🖼️", SystemCategory, ActionUtil.VK_SNAPSHOT),
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
    /// Additional arguments for Open actions.
    /// </summary>
    [ObservableProperty]
    private string _arguments = string.Empty;

    /// <summary>
    /// Optional working directory for Open actions.
    /// </summary>
    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    /// <summary>
    /// The inline script body for Script actions.
    /// </summary>
    [ObservableProperty]
    private string _script = string.Empty;

    /// <summary>
    /// For Script actions, runs the interpreter without showing a console window.
    /// </summary>
    [ObservableProperty]
    private bool _runHidden;

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
    /// Creates an Open action.
    /// </summary>
    public static PieAction CreateOpenAction(string name, string target, string icon = "📁", string arguments = "", string workingDirectory = "")
        => new(name, icon)
        {
            Type = ActionType.Open,
            Parameter = target,
            Arguments = arguments,
            WorkingDirectory = workingDirectory
        };

    /// <summary>
    /// Creates a script action.
    /// </summary>
    public static PieAction CreateScriptAction(string name, string script, string icon = DefaultIcon, string interpreter = "", string workingDirectory = "", bool runHidden = false)
        => new(name, icon)
        {
            Type = ActionType.Script,
            Parameter = interpreter,
            Script = script,
            WorkingDirectory = workingDirectory,
            RunHidden = runHidden
        };

    /// <summary>
    /// Creates a copy of this action with the same configuration.
    /// </summary>
    public PieAction Clone() => new()
    {
        Name = Name,
        Icon = Icon,
        Type = Type,
        IsEnabled = IsEnabled,
        Parameter = Parameter,
        Arguments = Arguments,
        WorkingDirectory = WorkingDirectory,
        Script = Script,
        RunHidden = RunHidden,
    };

    /// <summary>
    /// Executes the action.
    /// </summary>
    public void Execute()
    {
        Log.Information($"Executing action: {Name} ({Type})");

        switch (Type)
        {
            case ActionType.None:
                throw new InvalidOperationException("No action configured");
            case ActionType.Key:
                ExecuteKey();
                return;
            case ActionType.Open:
                ExecuteOpen();
                return;
            case ActionType.Script:
                ExecuteScript();
                return;
            default:
                throw new NotSupportedException("Action type is not supported");
        }
    }

    private void ExecuteKey()
    {
        if (string.IsNullOrWhiteSpace(Parameter))
            throw new InvalidOperationException("Shortcut not configured");

        if (TryGetKeyAction(Parameter, out var definition))
        {
            ActionUtil.SimulateKey(definition.VirtualKey);
            return;
        }

        if (!HotkeyUtil.TryParse(Parameter, out _, out _))
            throw new InvalidOperationException("Shortcut is invalid");

        ActionUtil.SimulateKeyboardShortcut(Parameter);
    }

    private void ExecuteOpen()
    {
        if (string.IsNullOrWhiteSpace(Parameter))
            throw new InvalidOperationException("Launch target not configured");

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

    private void ExecuteScript()
    {
        if (string.IsNullOrWhiteSpace(Script))
            throw new InvalidOperationException("Script is empty");

        var interpreter = string.IsNullOrWhiteSpace(Parameter) ? DefaultScriptInterpreter : Parameter;
        var arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {EncodePowerShellCommand(Script)}";

        // The whole command line must fit CreateProcess's 32,767 character limit; fail with a clear message instead of an opaque OS error.
        if (interpreter.Length + 1 + arguments.Length >= 32000)
            throw new InvalidOperationException("Script is too long to run");

        // UseShellExecute must be false so CreateNoWindow can suppress the console window for hidden scripts.
        // The body is passed as a Base64-encoded command so multiline scripts, quotes, and newlines need no escaping and no temp file.
        var psi = new ProcessStartInfo(interpreter)
        {
            UseShellExecute = false,
            CreateNoWindow = RunHidden,
            Arguments = arguments
        };

        if (!string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            psi.WorkingDirectory = WorkingDirectory;
        }

        using var process = Process.Start(psi);
    }

    // PowerShell -EncodedCommand expects Base64 of the UTF-16LE bytes of the script.
    internal static string EncodePowerShellCommand(string script)
        => Convert.ToBase64String(Encoding.Unicode.GetBytes(script ?? string.Empty));

    public override string ToString() => Name;
}
