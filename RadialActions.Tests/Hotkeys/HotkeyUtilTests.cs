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
}
