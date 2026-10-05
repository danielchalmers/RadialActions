using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using RadialActions.Properties;

namespace RadialActions;

public sealed partial class GeneralSettingsViewModel : ObservableObject
{
    public const string MissingModifierHint = "Include Ctrl, Alt, or Win, such as Ctrl+Alt+Space.";
    public const string UnsupportedKeyHint = "Radial Actions can't use that key. Try a combination, such as Ctrl+Alt+Space.";
    public const string ActivationHotkeyIdleDescription = "Opens the menu from anywhere. Select the shortcut to change it.";
    public const string ActivationHotkeyRecordingDescription = "Press the new key combination, such as Ctrl+Alt+Space. Press Esc to cancel or Backspace to clear.";

    private readonly IStartupRegistration _startupRegistration;

    public GeneralSettingsViewModel(Settings settings)
        : this(settings, StartupRegistration.ForCurrentApp())
    {
    }

    internal GeneralSettingsViewModel(Settings settings, IStartupRegistration startupRegistration)
    {
        Settings = settings;
        _startupRegistration = startupRegistration;

        // Settings outlives every Settings window, so a weak subscription keeps closed windows collectable.
        PropertyChangedEventManager.AddHandler(settings, OnActivationHotkeyChanged, nameof(Settings.ActivationHotkey));
    }

    public Settings Settings { get; }

    /// <summary>
    /// Whether this copy starts when the user signs in. It's read from Windows instead of saved settings, so it also reflects Task Manager, a moved folder, or another copy taking over.
    /// </summary>
    public bool RunOnStartup
    {
        get => _startupRegistration.IsEnabled;
        set
        {
            if (value != _startupRegistration.IsEnabled)
            {
                _startupRegistration.TrySetEnabled(value);
            }

            // Raised even when Windows refused the change, so the toggle shows what will actually happen.
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// True while the activation hotkey button is waiting for a new combination. Recording only starts when the user selects the button, so focus landing on it can't change the hotkey.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActivationHotkeyDescription))]
    [NotifyPropertyChangedFor(nameof(ActivationHotkeyAccessibleName))]
    private bool _isRecordingActivationHotkey;

    /// <summary>
    /// The keys of the activation hotkey in order, shown as keycaps; empty when no hotkey is set.
    /// </summary>
    public IReadOnlyList<string> ActivationHotkeyKeys => SplitHotkey(Settings.ActivationHotkey);

    public bool HasActivationHotkey => ActivationHotkeyKeys.Count > 0;

    public string ActivationHotkeyDescription => IsRecordingActivationHotkey ? ActivationHotkeyRecordingDescription : ActivationHotkeyIdleDescription;

    public string ActivationHotkeyAccessibleName
    {
        get
        {
            if (IsRecordingActivationHotkey)
                return "Activation hotkey, press a key combination";

            return HasActivationHotkey ? $"Activation hotkey, {string.Join("+", ActivationHotkeyKeys)}" : "Activation hotkey, not set";
        }
    }

    /// <summary>
    /// Why the last combination pressed while recording the activation hotkey wasn't used, or null. Cleared when recording starts, ends or accepts a combination.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActivationHotkeyHint))]
    private string _activationHotkeyHint;

    public bool HasActivationHotkeyHint => !string.IsNullOrEmpty(ActivationHotkeyHint);

    /// <summary>
    /// Makes a combination pressed while recording the new activation hotkey and ends recording when it's safe to register globally; otherwise keeps the current hotkey, keeps recording and explains why.
    /// </summary>
    /// <returns>Whether the combination became the activation hotkey.</returns>
    public bool RecordActivationHotkey(ModifierKeys modifiers, Key key)
    {
        // A modifier on its own is a combination still being pressed, not a mistake.
        if (key == Key.None || HotkeyUtil.IsModifierKey(key))
            return false;

        // Keys without a hotkey name include an IME's processed keys, dead keys, the ISO <> key and Ctrl+Break.
        var hotkey = HotkeyUtil.BuildHotkeyString(key, modifiers);
        if (string.IsNullOrEmpty(hotkey))
        {
            ShowActivationHotkeyHint(UnsupportedKeyHint);
            return false;
        }

        if (!HotkeyUtil.IsValidActivationHotkey(modifiers, key))
        {
            ShowActivationHotkeyHint(GetInvalidActivationHotkeyHint(modifiers, key));
            return false;
        }

        ActivationHotkeyHint = null;
        Settings.ActivationHotkey = hotkey;
        IsRecordingActivationHotkey = false;
        return true;
    }

    public void ClearActivationHotkey()
    {
        ActivationHotkeyHint = null;
        Settings.ActivationHotkey = string.Empty;
        IsRecordingActivationHotkey = false;
    }

    public void StartRecordingActivationHotkey()
    {
        ActivationHotkeyHint = null;
        IsRecordingActivationHotkey = true;
    }

    /// <summary>
    /// Leaves recording without changing the hotkey, for example on Esc, a second click or focus moving away.
    /// </summary>
    public void CancelRecordingActivationHotkey()
    {
        ActivationHotkeyHint = null;
        IsRecordingActivationHotkey = false;
    }

    private void ShowActivationHotkeyHint(string hint)
    {
        // Resetting first raises a change even when the same hint is already showing, so every rejected press is announced again.
        ActivationHotkeyHint = null;
        ActivationHotkeyHint = hint;
    }

    internal static IReadOnlyList<string> SplitHotkey(string hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey))
            return [];

        // Key names never contain a plus sign (the + key is named "Equals"), so it only ever separates keys.
        return hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private void OnActivationHotkeyChanged(object sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ActivationHotkeyKeys));
        OnPropertyChanged(nameof(HasActivationHotkey));
        OnPropertyChanged(nameof(ActivationHotkeyAccessibleName));
    }

    internal static string GetInvalidActivationHotkeyHint(ModifierKeys modifiers, Key key)
    {
        if (HotkeyUtil.IsReservedSystemHotkey(modifiers, key))
            return $"Windows already uses {HotkeyUtil.BuildHotkeyString(key, modifiers)}. Try another combination, such as Ctrl+Alt+Space.";

        return MissingModifierHint;
    }
}
