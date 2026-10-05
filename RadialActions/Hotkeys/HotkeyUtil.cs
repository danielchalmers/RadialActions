using System.Windows.Input;

namespace RadialActions;

public static class HotkeyUtil
{
    private static readonly Dictionary<string, ModifierKeys> ModifierMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = ModifierKeys.Control,
        ["Control"] = ModifierKeys.Control,
        ["Alt"] = ModifierKeys.Alt,
        ["Shift"] = ModifierKeys.Shift,
        ["Win"] = ModifierKeys.Windows,
        ["Windows"] = ModifierKeys.Windows,
    };

    private static readonly Dictionary<string, Key> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = Key.Space,
        ["Enter"] = Key.Enter,
        ["Tab"] = Key.Tab,
        ["Backspace"] = Key.Back,
        ["Back"] = Key.Back,
        ["Delete"] = Key.Delete,
        ["Del"] = Key.Delete,
        ["Insert"] = Key.Insert,
        ["Ins"] = Key.Insert,
        ["Home"] = Key.Home,
        ["End"] = Key.End,
        ["PageUp"] = Key.PageUp,
        ["PgUp"] = Key.PageUp,
        ["PageDown"] = Key.PageDown,
        ["PgDn"] = Key.PageDown,
        ["Up"] = Key.Up,
        ["Down"] = Key.Down,
        ["Left"] = Key.Left,
        ["Right"] = Key.Right,
        ["Escape"] = Key.Escape,
        ["Esc"] = Key.Escape,
        ["PrintScreen"] = Key.PrintScreen,
        ["PrtSc"] = Key.PrintScreen,
        ["ScrollLock"] = Key.Scroll,
        ["Pause"] = Key.Pause,
        ["NumLock"] = Key.NumLock,
        ["CapsLock"] = Key.CapsLock,
        ["Add"] = Key.Add,
        ["Subtract"] = Key.Subtract,
        ["Multiply"] = Key.Multiply,
        ["Divide"] = Key.Divide,
        ["Decimal"] = Key.Decimal,
        ["Semicolon"] = Key.OemSemicolon,
        ["Equals"] = Key.OemPlus,
        ["Comma"] = Key.OemComma,
        ["Minus"] = Key.OemMinus,
        ["Period"] = Key.OemPeriod,
        ["Slash"] = Key.OemQuestion,
        ["Grave"] = Key.OemTilde,
        ["OpenBracket"] = Key.OemOpenBrackets,
        ["Backslash"] = Key.OemPipe,
        ["CloseBracket"] = Key.OemCloseBrackets,
        ["Quote"] = Key.OemQuotes,
    };

    public static bool TryParse(string hotkey, out ModifierKeys modifiers, out Key key)
    {
        modifiers = ModifierKeys.None;
        key = Key.None;

        if (string.IsNullOrWhiteSpace(hotkey))
            return false;

        var parts = hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (ModifierMap.TryGetValue(part, out var modifier))
            {
                modifiers |= modifier;
                continue;
            }

            if (TryParseKey(part, out var parsedKey))
            {
                key = parsedKey;
                continue;
            }

            return false;
        }

        return key != Key.None;
    }

    /// <summary>
    /// Returns whether a key is part of a hotkey combination, either as its main key or as one of its modifiers.
    /// </summary>
    public static bool IsHotkeyComponent(Key key, ModifierKeys modifiers, Key hotkeyKey)
    {
        if (key == hotkeyKey && key != Key.None)
            return true;

        return key switch
        {
            Key.LeftCtrl or Key.RightCtrl => modifiers.HasFlag(ModifierKeys.Control),
            Key.LeftAlt or Key.RightAlt => modifiers.HasFlag(ModifierKeys.Alt),
            Key.LeftShift or Key.RightShift => modifiers.HasFlag(ModifierKeys.Shift),
            Key.LWin or Key.RWin => modifiers.HasFlag(ModifierKeys.Windows),
            _ => false
        };
    }

    public static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin;
    }

    /// <summary>
    /// Returns whether a recorder box should let a key press keep its usual meaning: Tab and Shift+Tab always move focus, and the activation hotkey box also lets a bare Escape and Alt+F4 through.
    /// </summary>
    public static bool IsRecorderPassThroughKey(Key key, ModifierKeys modifiers, bool isActivationHotkey)
    {
        if (key == Key.Tab && (modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0)
            return true;

        if (!isActivationHotkey)
            return false;

        return (key == Key.Escape && modifiers == ModifierKeys.None) || (key == Key.F4 && modifiers == ModifierKeys.Alt);
    }

    /// <summary>
    /// Returns whether a key combination is one Windows keeps for itself, so registering it as a global hotkey can never work.
    /// </summary>
    public static bool IsReservedSystemHotkey(ModifierKeys modifiers, Key key)
    {
        // Windows keeps F12 and Shift+F12 for debuggers, so RegisterHotKey always refuses them; F12 with Ctrl, Alt or Win registers normally.
        if (key == Key.F12 && modifiers is ModifierKeys.None or ModifierKeys.Shift)
            return true;

        return (modifiers, key) switch
        {
            (ModifierKeys.Alt, Key.F4) => true,
            (ModifierKeys.Alt, Key.Tab) => true,
            (ModifierKeys.Alt | ModifierKeys.Shift, Key.Tab) => true,
            (ModifierKeys.Alt, Key.Escape) => true,
            (ModifierKeys.Control, Key.Escape) => true,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.Escape) => true,
            (ModifierKeys.Windows, Key.L) => true,
            (ModifierKeys.Control | ModifierKeys.Alt, Key.Delete) => true,
            _ => false
        };
    }

    /// <summary>
    /// Returns whether a recorded combination is safe as the global activation hotkey: any key with Ctrl, Alt or Win except chords Windows reserves, otherwise only F1-F24, Pause and Scroll Lock so a stray letter, Space or Tab can't take over the keyboard system-wide.
    /// </summary>
    public static bool IsValidActivationHotkey(ModifierKeys modifiers, Key key)
    {
        if (key == Key.None || IsModifierKey(key) || IsReservedSystemHotkey(modifiers, key))
            return false;

        if ((modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0)
            return true;

        return key is >= Key.F1 and <= Key.F24 or Key.Pause or Key.Scroll;
    }

    /// <summary>
    /// Returns whether a combination is a key people type or move around with (letters, digits, Space, Enter, Tab, Escape, Backspace, arrows, Home, End, Page Up, Page Down or punctuation) with no Ctrl, Alt or Win, so registering it globally would take that key away from every app.
    /// </summary>
    public static bool IsTypingOrNavigationHotkey(ModifierKeys modifiers, Key key)
    {
        if ((modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0)
            return false;

        return key is >= Key.A and <= Key.Z
            or >= Key.D0 and <= Key.D9
            or Key.Space or Key.Enter or Key.Tab or Key.Escape or Key.Back
            or Key.Left or Key.Up or Key.Right or Key.Down
            or Key.Home or Key.End or Key.PageUp or Key.PageDown
            or >= Key.OemSemicolon and <= Key.OemBackslash;
    }

    /// <summary>
    /// Returns whether a saved activation hotkey must be replaced on load: a typing or navigation key without Ctrl, Alt or Win (older versions could record one) or a chord Windows reserves. Other saved keys the recorder no longer accepts on their own, such as Insert or NumPad keys, keep working.
    /// </summary>
    public static bool ShouldReplaceSavedActivationHotkey(string hotkey)
    {
        return TryParse(hotkey, out var modifiers, out var key)
            && (IsTypingOrNavigationHotkey(modifiers, key) || IsReservedSystemHotkey(modifiers, key));
    }

    public static string BuildHotkeyString(Key key, ModifierKeys modifiers)
    {
        if (key == Key.None)
            return string.Empty;

        var parts = new List<string>(4);
        if ((modifiers & ModifierKeys.Control) != 0)
            parts.Add("Ctrl");
        if ((modifiers & ModifierKeys.Alt) != 0)
            parts.Add("Alt");
        if ((modifiers & ModifierKeys.Shift) != 0)
            parts.Add("Shift");
        if ((modifiers & ModifierKeys.Windows) != 0)
            parts.Add("Win");

        var keyName = GetKeyName(key);
        if (string.IsNullOrWhiteSpace(keyName))
            return string.Empty;

        parts.Add(keyName);
        return string.Join("+", parts);
    }

    public static string GetKeyName(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
            return key.ToString();

        if (key is >= Key.D0 and <= Key.D9)
            return ((int)(key - Key.D0)).ToString();

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
            return key.ToString();

        if (key is >= Key.F1 and <= Key.F24)
            return key.ToString();

        return key switch
        {
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.Tab => "Tab",
            Key.Back => "Backspace",
            Key.Escape => "Escape",
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.PrintScreen => "PrintScreen",
            Key.Scroll => "ScrollLock",
            Key.Pause => "Pause",
            Key.NumLock => "NumLock",
            Key.CapsLock => "CapsLock",
            Key.Add => "Add",
            Key.Subtract => "Subtract",
            Key.Multiply => "Multiply",
            Key.Divide => "Divide",
            Key.Decimal => "Decimal",
            Key.OemSemicolon or Key.Oem1 => "Semicolon",
            Key.OemPlus => "Equals",
            Key.OemComma => "Comma",
            Key.OemMinus => "Minus",
            Key.OemPeriod => "Period",
            Key.OemQuestion or Key.Oem2 => "Slash",
            Key.OemTilde or Key.Oem3 => "Grave",
            Key.OemOpenBrackets or Key.Oem4 => "OpenBracket",
            Key.OemPipe or Key.Oem5 => "Backslash",
            Key.OemCloseBrackets or Key.Oem6 => "CloseBracket",
            Key.OemQuotes or Key.Oem7 => "Quote",
            _ => string.Empty
        };
    }

    private static bool TryParseKey(string token, out Key key)
    {
        key = Key.None;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        if (NamedKeys.TryGetValue(token, out key))
            return true;

        if (token.Length == 1)
        {
            var c = char.ToUpperInvariant(token[0]);
            if (c >= 'A' && c <= 'Z')
            {
                key = (Key)((int)Key.A + (c - 'A'));
                return true;
            }

            if (c >= '0' && c <= '9')
            {
                key = (Key)((int)Key.D0 + (c - '0'));
                return true;
            }
        }

        if (token.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(token.AsSpan(1), out var fNum) &&
            fNum >= 1 && fNum <= 24)
        {
            key = (Key)((int)Key.F1 + (fNum - 1));
            return true;
        }

        if (token.StartsWith("NumPad", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(token.AsSpan(6), out var padNum) &&
            padNum >= 0 && padNum <= 9)
        {
            key = (Key)((int)Key.NumPad0 + padNum);
            return true;
        }

        return Enum.TryParse(token, true, out key);
    }
}
