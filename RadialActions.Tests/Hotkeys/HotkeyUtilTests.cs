using System.Windows.Input;

namespace RadialActions.Tests;

public class HotkeyUtilTests
{
    [Fact]
    public void TryParse_ValidHotkey_ParsesModifiersAndKey()
    {
        var ok = HotkeyUtil.TryParse("Ctrl+Alt+Space", out var modifiers, out var key);

        Assert.True(ok);
        Assert.Equal(ModifierKeys.Control | ModifierKeys.Alt, modifiers);
        Assert.Equal(Key.Space, key);
    }

    [Fact]
    public void TryParse_InvalidToken_ReturnsFalse()
    {
        var ok = HotkeyUtil.TryParse("Ctrl+Nope+K", out _, out _);

        Assert.False(ok);
    }

    [Theory]
    [InlineData(Key.Space, true)]
    [InlineData(Key.LeftCtrl, true)]
    [InlineData(Key.RightCtrl, true)]
    [InlineData(Key.LeftAlt, true)]
    [InlineData(Key.RightAlt, true)]
    [InlineData(Key.LeftShift, false)]
    [InlineData(Key.LWin, false)]
    [InlineData(Key.A, false)]
    public void IsHotkeyComponent_CtrlAltSpace_MatchesMainKeyAndModifiers(Key key, bool expected)
    {
        var isComponent = HotkeyUtil.IsHotkeyComponent(key, ModifierKeys.Control | ModifierKeys.Alt, Key.Space);

        Assert.Equal(expected, isComponent);
    }

    [Fact]
    public void IsHotkeyComponent_NoneKey_NeverMatches()
    {
        Assert.False(HotkeyUtil.IsHotkeyComponent(Key.None, ModifierKeys.None, Key.None));
    }

    [Theory]
    [InlineData(Key.Space, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(Key.F12, ModifierKeys.Shift)]
    [InlineData(Key.D7, ModifierKeys.None)]
    public void BuildAndParse_RoundTrips(Key key, ModifierKeys modifiers)
    {
        var hotkey = HotkeyUtil.BuildHotkeyString(key, modifiers);
        var ok = HotkeyUtil.TryParse(hotkey, out var parsedModifiers, out var parsedKey);

        Assert.True(ok);
        Assert.Equal(modifiers, parsedModifiers);
        Assert.Equal(key, parsedKey);
    }

    [Theory]
    [InlineData(Key.LeftCtrl, true)]
    [InlineData(Key.RightCtrl, true)]
    [InlineData(Key.LeftShift, true)]
    [InlineData(Key.RightShift, true)]
    [InlineData(Key.LeftAlt, true)]
    [InlineData(Key.RightAlt, true)]
    [InlineData(Key.LWin, true)]
    [InlineData(Key.RWin, true)]
    [InlineData(Key.Space, false)]
    [InlineData(Key.A, false)]
    [InlineData(Key.System, false)]
    public void IsModifierKey_MatchesOnlyModifierKeys(Key key, bool expected)
    {
        Assert.Equal(expected, HotkeyUtil.IsModifierKey(key));
    }

    [Theory]
    [InlineData(Key.Space, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(Key.A, ModifierKeys.Control)]
    [InlineData(Key.D1, ModifierKeys.Alt)]
    [InlineData(Key.Space, ModifierKeys.Windows)]
    [InlineData(Key.Tab, ModifierKeys.Control)]
    [InlineData(Key.Escape, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(Key.F4, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(Key.L, ModifierKeys.Windows | ModifierKeys.Shift)]
    [InlineData(Key.Delete, ModifierKeys.Control)]
    [InlineData(Key.OemPeriod, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.Up, ModifierKeys.Alt | ModifierKeys.Shift)]
    [InlineData(Key.F1, ModifierKeys.None)]
    [InlineData(Key.F11, ModifierKeys.None)]
    [InlineData(Key.F13, ModifierKeys.None)]
    [InlineData(Key.F24, ModifierKeys.None)]
    [InlineData(Key.F9, ModifierKeys.Shift)]
    [InlineData(Key.Pause, ModifierKeys.None)]
    [InlineData(Key.Scroll, ModifierKeys.None)]
    [InlineData(Key.Pause, ModifierKeys.Shift)]
    public void IsValidActivationHotkey_AcceptsSafeCombinations(Key key, ModifierKeys modifiers)
    {
        Assert.True(HotkeyUtil.IsValidActivationHotkey(modifiers, key));
    }

    [Theory]
    [InlineData(Key.A, ModifierKeys.None)]
    [InlineData(Key.Z, ModifierKeys.Shift)]
    [InlineData(Key.D7, ModifierKeys.None)]
    [InlineData(Key.D7, ModifierKeys.Shift)]
    [InlineData(Key.Space, ModifierKeys.None)]
    [InlineData(Key.Space, ModifierKeys.Shift)]
    [InlineData(Key.Enter, ModifierKeys.None)]
    [InlineData(Key.Tab, ModifierKeys.None)]
    [InlineData(Key.Tab, ModifierKeys.Shift)]
    [InlineData(Key.Escape, ModifierKeys.None)]
    [InlineData(Key.Back, ModifierKeys.None)]
    [InlineData(Key.Delete, ModifierKeys.None)]
    [InlineData(Key.Insert, ModifierKeys.None)]
    [InlineData(Key.Left, ModifierKeys.None)]
    [InlineData(Key.Up, ModifierKeys.Shift)]
    [InlineData(Key.Home, ModifierKeys.None)]
    [InlineData(Key.End, ModifierKeys.None)]
    [InlineData(Key.PageUp, ModifierKeys.None)]
    [InlineData(Key.PageDown, ModifierKeys.Shift)]
    [InlineData(Key.OemComma, ModifierKeys.None)]
    [InlineData(Key.OemQuestion, ModifierKeys.Shift)]
    [InlineData(Key.NumPad5, ModifierKeys.None)]
    [InlineData(Key.PrintScreen, ModifierKeys.None)]
    [InlineData(Key.CapsLock, ModifierKeys.None)]
    [InlineData(Key.None, ModifierKeys.Control)]
    [InlineData(Key.LeftCtrl, ModifierKeys.Control)]
    [InlineData(Key.LeftShift, ModifierKeys.Shift)]
    public void IsValidActivationHotkey_RejectsKeysWithoutCtrlAltOrWinThatPeopleType(Key key, ModifierKeys modifiers)
    {
        Assert.False(HotkeyUtil.IsValidActivationHotkey(modifiers, key));
    }

    [Theory]
    [InlineData(Key.F4, ModifierKeys.Alt)]
    [InlineData(Key.Tab, ModifierKeys.Alt)]
    [InlineData(Key.Tab, ModifierKeys.Alt | ModifierKeys.Shift)]
    [InlineData(Key.Escape, ModifierKeys.Alt)]
    [InlineData(Key.Escape, ModifierKeys.Control)]
    [InlineData(Key.Escape, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.L, ModifierKeys.Windows)]
    [InlineData(Key.Delete, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(Key.F12, ModifierKeys.None)]
    [InlineData(Key.F12, ModifierKeys.Shift)]
    public void IsValidActivationHotkey_RejectsReservedSystemChords(Key key, ModifierKeys modifiers)
    {
        Assert.True(HotkeyUtil.IsReservedSystemHotkey(modifiers, key));
        Assert.False(HotkeyUtil.IsValidActivationHotkey(modifiers, key));
    }

    [Theory]
    [InlineData(Key.A, ModifierKeys.None)]
    [InlineData(Key.Z, ModifierKeys.Shift)]
    [InlineData(Key.D0, ModifierKeys.None)]
    [InlineData(Key.D9, ModifierKeys.Shift)]
    [InlineData(Key.Space, ModifierKeys.None)]
    [InlineData(Key.Enter, ModifierKeys.None)]
    [InlineData(Key.Tab, ModifierKeys.None)]
    [InlineData(Key.Tab, ModifierKeys.Shift)]
    [InlineData(Key.Escape, ModifierKeys.None)]
    [InlineData(Key.Back, ModifierKeys.None)]
    [InlineData(Key.Left, ModifierKeys.None)]
    [InlineData(Key.Up, ModifierKeys.Shift)]
    [InlineData(Key.Right, ModifierKeys.None)]
    [InlineData(Key.Down, ModifierKeys.None)]
    [InlineData(Key.Home, ModifierKeys.None)]
    [InlineData(Key.End, ModifierKeys.None)]
    [InlineData(Key.PageUp, ModifierKeys.None)]
    [InlineData(Key.PageDown, ModifierKeys.Shift)]
    [InlineData(Key.OemSemicolon, ModifierKeys.None)]
    [InlineData(Key.OemComma, ModifierKeys.None)]
    [InlineData(Key.OemQuotes, ModifierKeys.Shift)]
    [InlineData(Key.OemBackslash, ModifierKeys.None)]
    public void IsTypingOrNavigationHotkey_KeysPeopleTypeWithoutCtrlAltOrWin_AreTypingKeys(Key key, ModifierKeys modifiers)
    {
        Assert.True(HotkeyUtil.IsTypingOrNavigationHotkey(modifiers, key));
    }

    [Theory]
    [InlineData(Key.A, ModifierKeys.Control)]
    [InlineData(Key.Space, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(Key.Tab, ModifierKeys.Alt)]
    [InlineData(Key.D1, ModifierKeys.Windows)]
    [InlineData(Key.Insert, ModifierKeys.None)]
    [InlineData(Key.Delete, ModifierKeys.None)]
    [InlineData(Key.NumPad5, ModifierKeys.None)]
    [InlineData(Key.Add, ModifierKeys.None)]
    [InlineData(Key.F1, ModifierKeys.None)]
    [InlineData(Key.F13, ModifierKeys.Shift)]
    [InlineData(Key.Pause, ModifierKeys.None)]
    [InlineData(Key.Scroll, ModifierKeys.None)]
    [InlineData(Key.PrintScreen, ModifierKeys.None)]
    [InlineData(Key.CapsLock, ModifierKeys.None)]
    [InlineData(Key.MediaPlayPause, ModifierKeys.None)]
    [InlineData(Key.VolumeMute, ModifierKeys.None)]
    [InlineData(Key.ImeProcessed, ModifierKeys.None)]
    public void IsTypingOrNavigationHotkey_OtherKeysAndCombinations_AreNotTypingKeys(Key key, ModifierKeys modifiers)
    {
        Assert.False(HotkeyUtil.IsTypingOrNavigationHotkey(modifiers, key));
    }

    [Theory]
    [InlineData("Tab")]
    [InlineData("Space")]
    [InlineData("Shift+A")]
    [InlineData("7")]
    [InlineData("Enter")]
    [InlineData("Esc")]
    [InlineData("Backspace")]
    [InlineData("Shift+Up")]
    [InlineData("PageDown")]
    [InlineData("Comma")]
    [InlineData("Alt+F4")]
    [InlineData("Alt+Tab")]
    [InlineData("Win+L")]
    [InlineData("Ctrl+Alt+Delete")]
    [InlineData("F12")]
    [InlineData("Shift+F12")]
    public void ShouldReplaceSavedActivationHotkey_TypingKeysAndReservedChords_AreReplaced(string hotkey)
    {
        Assert.True(HotkeyUtil.ShouldReplaceSavedActivationHotkey(hotkey));
    }

    [Theory]
    [InlineData("Ctrl+Alt+Space")]
    [InlineData("Win+Space")]
    [InlineData("Ctrl+Shift+K")]
    [InlineData("Insert")]
    [InlineData("Delete")]
    [InlineData("NumPad5")]
    [InlineData("F13")]
    [InlineData("Shift+F9")]
    [InlineData("Pause")]
    [InlineData("ScrollLock")]
    [InlineData("PrintScreen")]
    [InlineData("MediaPlayPause")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Ctrl+Nope")]
    [InlineData("Ctrl+Alt+F12")]
    public void ShouldReplaceSavedActivationHotkey_OtherHotkeys_AreKept(string hotkey)
    {
        Assert.False(HotkeyUtil.ShouldReplaceSavedActivationHotkey(hotkey));
    }

    [Theory]
    [InlineData(Key.Space, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(Key.F4, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(Key.F4, ModifierKeys.None)]
    [InlineData(Key.Tab, ModifierKeys.Control)]
    [InlineData(Key.L, ModifierKeys.Control | ModifierKeys.Windows)]
    [InlineData(Key.Delete, ModifierKeys.Control)]
    [InlineData(Key.F12, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(Key.F12, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)]
    public void IsReservedSystemHotkey_LeavesOtherCombinationsAlone(Key key, ModifierKeys modifiers)
    {
        Assert.False(HotkeyUtil.IsReservedSystemHotkey(modifiers, key));
    }

    [Theory]
    [InlineData(Key.Tab, ModifierKeys.None, false, true)]
    [InlineData(Key.Tab, ModifierKeys.Shift, false, true)]
    [InlineData(Key.Tab, ModifierKeys.None, true, true)]
    [InlineData(Key.Tab, ModifierKeys.Shift, true, true)]
    [InlineData(Key.Tab, ModifierKeys.Control, false, false)]
    [InlineData(Key.Tab, ModifierKeys.Control, true, false)]
    [InlineData(Key.Tab, ModifierKeys.Alt, true, false)]
    [InlineData(Key.Escape, ModifierKeys.None, true, true)]
    [InlineData(Key.Escape, ModifierKeys.None, false, false)]
    [InlineData(Key.Escape, ModifierKeys.Control, true, false)]
    [InlineData(Key.F4, ModifierKeys.Alt, true, true)]
    [InlineData(Key.F4, ModifierKeys.Alt, false, false)]
    [InlineData(Key.F4, ModifierKeys.Control | ModifierKeys.Alt, true, false)]
    [InlineData(Key.A, ModifierKeys.None, true, false)]
    [InlineData(Key.Space, ModifierKeys.Control | ModifierKeys.Alt, true, false)]
    public void IsRecorderPassThroughKey_LetsFocusAndCloseKeysThrough(Key key, ModifierKeys modifiers, bool isActivationHotkey, bool expected)
    {
        Assert.Equal(expected, HotkeyUtil.IsRecorderPassThroughKey(key, modifiers, isActivationHotkey));
    }
}
