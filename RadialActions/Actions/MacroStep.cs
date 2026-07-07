using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace RadialActions;

/// <summary>
/// Defines the kind of work a single macro step performs.
/// </summary>
public enum MacroStepType
{
    /// <summary>
    /// Simulate a keyboard shortcut or a predefined key action.
    /// </summary>
    Shortcut = 0,

    /// <summary>
    /// Type a string of text into the focused window.
    /// </summary>
    Text = 1,

    /// <summary>
    /// Wait before running the next step.
    /// </summary>
    Delay = 2,
}

/// <summary>
/// Represents a single step in a macro action.
/// </summary>
public partial class MacroStep : ObservableObject
{
    public const int DefaultDelayMilliseconds = 500;
    public const int MaxDelayMilliseconds = 60_000;

    /// <summary>
    /// The kind of work this step performs.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationError))]
    private MacroStepType _type = MacroStepType.Shortcut;

    /// <summary>
    /// The shortcut (for example "Ctrl+C" or "PlayPause") or the text to type.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationError))]
    private string _value = string.Empty;

    /// <summary>
    /// How long a delay step waits, in milliseconds.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationError))]
    private int _delayMilliseconds = DefaultDelayMilliseconds;

    /// <summary>
    /// A human-readable reason the step can't run, or <c>null</c> when the step is valid.
    /// </summary>
    [JsonIgnore]
    public string ValidationError => GetValidationError(this);

    /// <summary>
    /// Returns why the step can't run, or <c>null</c> when the step is valid.
    /// </summary>
    public static string GetValidationError(MacroStep step)
    {
        if (step == null)
        {
            return "Step is missing";
        }

        switch (step.Type)
        {
            case MacroStepType.Shortcut:
                if (string.IsNullOrWhiteSpace(step.Value))
                    return "Enter a shortcut, such as Ctrl+C";
                if (!PieAction.TryGetKeyAction(step.Value, out _) && !HotkeyUtil.TryParse(step.Value, out _, out _))
                    return $"\"{step.Value}\" is not a valid shortcut";
                return null;
            case MacroStepType.Text:
                if (string.IsNullOrEmpty(step.Value))
                    return "Enter the text to type";
                return null;
            case MacroStepType.Delay:
                if (step.DelayMilliseconds <= 0)
                    return "Delay must be greater than zero";
                if (step.DelayMilliseconds > MaxDelayMilliseconds)
                    return $"Delay can't exceed {MaxDelayMilliseconds} milliseconds";
                return null;
            default:
                return "Step type is not supported";
        }
    }

    /// <summary>
    /// Repairs missing or stale values loaded from an old or hand-edited settings file.
    /// </summary>
    public void NormalizeAfterLoad()
    {
        Value ??= string.Empty;

        if (!Enum.IsDefined(typeof(MacroStepType), Type))
        {
            Type = MacroStepType.Shortcut;
        }

        if (DelayMilliseconds <= 0 || DelayMilliseconds > MaxDelayMilliseconds)
        {
            DelayMilliseconds = DefaultDelayMilliseconds;
        }
    }

    /// <summary>
    /// Creates a copy of this step.
    /// </summary>
    public MacroStep Clone() => new()
    {
        Type = Type,
        Value = Value,
        DelayMilliseconds = DelayMilliseconds,
    };
}
