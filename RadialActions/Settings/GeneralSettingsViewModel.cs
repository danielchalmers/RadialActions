using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using RadialActions.Properties;

namespace RadialActions;

public sealed partial class GeneralSettingsViewModel : ObservableObject
{
    public const string MissingModifierHint = "Include Ctrl, Alt, or Win, such as Ctrl+Alt+Space.";
    public const string UnsupportedKeyHint = "Radial Actions can't use that key. Try a combination, such as Ctrl+Alt+Space.";

    public GeneralSettingsViewModel(Settings settings)
    {
        Settings = settings;
    }

    public Settings Settings { get; }

    /// <summary>
    /// Why the last combination pressed in the activation hotkey box wasn't used, or null. Cleared by the next valid recording or when the box loses focus.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActivationHotkeyHint))]
    private string _activationHotkeyHint;

    public bool HasActivationHotkeyHint => !string.IsNullOrEmpty(ActivationHotkeyHint);

    /// <summary>
    /// Makes a combination pressed in the activation hotkey box the new hotkey when it's safe to register globally; otherwise keeps the current hotkey and explains why.
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
        return true;
    }

    public void ClearActivationHotkey()
    {
        ActivationHotkeyHint = null;
        Settings.ActivationHotkey = string.Empty;
    }

    public void ClearActivationHotkeyHint()
    {
        ActivationHotkeyHint = null;
    }

    private void ShowActivationHotkeyHint(string hint)
    {
        // Resetting first raises a change even when the same hint is already showing, so every rejected press is announced again.
        ActivationHotkeyHint = null;
        ActivationHotkeyHint = hint;
    }

    internal static string GetInvalidActivationHotkeyHint(ModifierKeys modifiers, Key key)
    {
        if (HotkeyUtil.IsReservedSystemHotkey(modifiers, key))
            return $"Windows already uses {HotkeyUtil.BuildHotkeyString(key, modifiers)}. Try another combination, such as Ctrl+Alt+Space.";

        return MissingModifierHint;
    }
}
